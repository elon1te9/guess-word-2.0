using Microsoft.AspNetCore.SignalR.Client;

namespace GuessWord.Client.Infrastructure.Realtime;

// Каждая страница/вкладка владеет своей подпиской. Сетевой разрыв не означает выход из комнаты.
public sealed class GameHubSubscription(HubConnection connection, Func<CancellationToken, Task> restore,
    Func<bool, Task> statusChanged) : IAsyncDisposable
{
    private readonly CancellationTokenSource lifetime = new();
    private Task? running;

    public void Start() => running ??= RunAsync();

    private async Task RunAsync()
    {
        var token = lifetime.Token;
        while (!token.IsCancellationRequested)
        {
            try
            {
                using var attempt = CancellationTokenSource.CreateLinkedTokenSource(token);
                attempt.CancelAfter(TimeSpan.FromSeconds(10));
                if (connection.State == HubConnectionState.Disconnected) await connection.StartAsync(attempt.Token);
                await restore(attempt.Token); // Сначала группы, затем авторитетный снимок.
                await statusChanged(true);
                while (connection.State == HubConnectionState.Connected)
                    await Task.Delay(500, token);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { break; }
            catch
            {
                if (token.IsCancellationRequested) break;
            }
            if (token.IsCancellationRequested) break;
            await statusChanged(false);
            try { await Task.Delay(2000, token); }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { break; }
        }
    }

    public async ValueTask DisposeAsync()
    {
        await lifetime.CancelAsync();
        if (running is not null) await running;
        await connection.DisposeAsync();
        lifetime.Dispose();
    }
}
