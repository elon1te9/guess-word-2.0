using GuessWord.Client.Infrastructure.Http;
using GuessWord.Client.State;
using Microsoft.AspNetCore.SignalR.Client;

namespace GuessWord.Client.Infrastructure.Realtime;

public class GameHubConnectionFactory(ApiSettings apiSettings, UserStateService userState)
{
    public HubConnection Create() => new HubConnectionBuilder()
        .WithUrl(new Uri(new Uri(apiSettings.BaseUrl), "hubs/game"), options =>
        {
            options.AccessTokenProvider = () => Task.FromResult(userState.Token);
        })
        .Build();
}
