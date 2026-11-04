using GuessWord.Api.Features.Auth;
using GuessWord.Api.Features.Leaderboard;
using GuessWord.Api.Data;
using GuessWord.Api.Realtime;
using GuessWord.Api.Features.Dictionary;
using GuessWord.Api.Features.Games;
using GuessWord.Api.Features.History;
using GuessWord.Api.Features.Rooms;
using GuessWord.Api.Features.Users;
using GuessWord.Api.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Pgvector.EntityFrameworkCore;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Hosting request diagnostics include the query string, which can contain a SignalR JWT.
builder.Logging.AddFilter("Microsoft.AspNetCore.Hosting.Diagnostics", LogLevel.Warning);

builder.Services.AddControllers();
builder.Services.AddScoped<GuessWord.Api.Features.Health.IDictionaryReadiness, GuessWord.Api.Features.Health.DictionaryReadiness>();
builder.Services.AddExceptionHandler<GuessWord.Api.Middleware.ApiExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddSignalR();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddMemoryCache(options =>
{
    options.SizeLimit = 100;
});

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        o => o.UseVector()),
    ServiceLifetime.Scoped);

builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<UserProfileService>();
builder.Services.AddScoped<LeaderboardService>();

builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<IGameService, GameService>();
builder.Services.AddScoped<GameQueries>();
builder.Services.AddScoped<SingleGameCommands>();
builder.Services.AddScoped<MultiplayerGameCommands>();
builder.Services.AddScoped<GameResponseBuilder>();
builder.Services.AddScoped<GameDataAccess>();
builder.Services.AddScoped<GameMutation>();
builder.Services.AddScoped<GuessHandler>();
builder.Services.AddScoped<GameHistoryService>();

builder.Services.AddScoped<IRoomService, RoomService>();
builder.Services.AddScoped<DictionaryImportService>();
builder.Services.AddScoped<IRankService, RankService>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                if (context.Request.Path.StartsWithSegments("/hubs/game"))
                {
                    var token = context.Request.Query["access_token"];
                    if (!string.IsNullOrEmpty(token)) context.Token = token;
                }
                return Task.CompletedTask;
            }
        };
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowBlazor",
        policy => policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseHttpsRedirection();
}

app.UseExceptionHandler();

// Only the combined publication has wwwroot; the existing API-only image still works.
if (Directory.Exists(app.Environment.WebRootPath))
{
    app.UseBlazorFrameworkFiles();
    app.UseStaticFiles(new StaticFileOptions
    {
        OnPrepareResponse = context =>
        {
            // Revalidate the entry point and configuration after each deployment.
            if (context.File.Name is "index.html" or "appsettings.json")
                context.Context.Response.Headers.CacheControl = "no-cache";
        }
    });
}

// Route after static-file middleware, including its terminal /_framework branch.
app.UseRouting();
app.UseCors("AllowBlazor");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<GameHub>("/hubs/game");

if (Directory.Exists(app.Environment.WebRootPath))
{
    // Reserve server and asset namespaces: missing endpoints must never return the SPA.
    app.MapFallback("{*path:nonfile}", async context =>
    {
        string[] reserved = ["/api", "/health", "/hubs", "/_framework", "/_content", "/css", "/js", "/fonts", "/images", "/swagger"];
        var requestPath = context.Request.PathBase.Add(context.Request.Path);
        if (reserved.Any(prefix => requestPath.StartsWithSegments(prefix))
            || !HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        context.Response.ContentType = "text/html; charset=utf-8";
        context.Response.Headers.CacheControl = "no-cache";
        if (!HttpMethods.IsHead(context.Request.Method))
            await context.Response.SendFileAsync(Path.Combine(app.Environment.WebRootPath, "index.html"));
    });
}

using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await context.Database.MigrateAsync();

    var importer = scope.ServiceProvider.GetRequiredService<DictionaryImportService>();
    if (!app.Environment.IsEnvironment("Testing"))
        await importer.ImportAsync();
}

app.Run();

public partial class Program { }
