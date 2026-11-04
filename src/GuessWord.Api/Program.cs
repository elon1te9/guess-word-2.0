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
app.UseCors("AllowBlazor");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<GameHub>("/hubs/game");

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
