using GuessWord.Client.Infrastructure.Realtime;
using GuessWord.Client;
using GuessWord.Client.Infrastructure.Http;

using GuessWord.Client.Infrastructure.Storage;
using GuessWord.Client.State;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var configuredApi = builder.Configuration["Api:BaseUrl"];
var siteUri = new Uri(builder.HostEnvironment.BaseAddress);
var baseUri = string.IsNullOrWhiteSpace(configuredApi)
    ? siteUri
    : new Uri(siteUri, configuredApi.TrimEnd('/') + "/");

builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = baseUri
});

builder.Services.AddScoped(_ => new ApiSettings
{
    BaseUrl = baseUri.ToString()
});

builder.Services.AddScoped<GameHubConnectionFactory>();
builder.Services.AddScoped<ApiRequestService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<GameService>();
builder.Services.AddScoped<RoomService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<LocalStorageService>();
builder.Services.AddSingleton<UserStateService>();

await builder.Build().RunAsync();
