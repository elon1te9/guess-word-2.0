using System.Net.Http;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using GuessWord.Contracts.Requests;
using GuessWord.Contracts.Responses;
using GuessWord.Contracts.Enums;
using Microsoft.AspNetCore.SignalR.Client;

namespace GuessWord.Client.Features.Rooms;

public partial class RoomPage
{
    [Parameter]
    public string Code { get; set; } = string.Empty;

    private RoomResponseDto? room;
    private string? copyMessage;
    private HubConnection? hubConnection;
    private Infrastructure.Realtime.GameHubSubscription? subscription;
    private bool disposed;
    private int snapshotRequest;
    private string? loadedRoute;
    private bool isLoading = true;
    private bool isLeaving;
    private bool isStartingGame;
    private string? errorMessage;
    private string? startGameError;
    private string? signalRErrorMessage;

    protected override async Task OnParametersSetAsync()
    {
        if (loadedRoute == Code) return;
        loadedRoute = Code;
        ++snapshotRequest;
        if (subscription is not null) await subscription.DisposeAsync();
        subscription = null;
        isLoading = true;
        if (!UserState.IsAuthorized)
        {
            Navigation.NavigateTo("/login");
            return;
        }

        await LoadRoomAsync();

        if (!disposed) await InitializeSignalRAsync();
    }

    private Task LoadRoomAsync() => LoadRoomAsync(CancellationToken.None);

    private async Task LoadRoomAsync(CancellationToken cancellationToken)
    {
        var request = ++snapshotRequest;
        var loadedRoom = await RoomService.GetRoomAsync(Code, cancellationToken);
        if (disposed || request != snapshotRequest) return;

        if (loadedRoom is null)
        {
            room = null;
            errorMessage = "Комната не найдена или доступ запрещён.";
            isLoading = false;
            return;
        }

        room = loadedRoom;
        errorMessage = null;
        isLoading = false;
        NavigateToGameIfStarted();
    }

    private async Task InitializeSignalRAsync()
    {
        hubConnection = HubConnections.Create();

        hubConnection.On<string>("RoomUpdated", async _ =>
        {
            startGameError = null;
            await LoadRoomAsync();
            await InvokeAsync(StateHasChanged);
        });

        // ID из события не используется для перехода: задержанное уведомление старого раунда
        // может только инициировать чтение текущего состояния комнаты.
        hubConnection.On<int>("MultiplayerGameStarted", _ => InvokeAsync(LoadRoomAsync));
        subscription = new(hubConnection, async token =>
        {
            await hubConnection.InvokeAsync("JoinRoomGroup", Code, token);
            await InvokeAsync(() => LoadRoomAsync(token));
            if (room is null) throw new HttpRequestException("Снимок комнаты недоступен.");
        }, connected => InvokeAsync(() =>
        {
            if (disposed) return;
            signalRErrorMessage = connected ? null : "Связь потеряна. Повторное подключение…";
            StateHasChanged();
        }));
        subscription.Start();
        await Task.CompletedTask;
    }

    private async Task CopyCodeAsync()
    {
        if (room is null) return;
        try
        {
            await JS.InvokeVoidAsync("navigator.clipboard.writeText", room.Code);
            copyMessage = "Код скопирован.";
        }
        catch (JSException)
        {
            copyMessage = "Выделите код и скопируйте его вручную.";
        }
    }

    private async Task StartMultiplayerGameAsync()
    {
        if (room is null || !room.IsHost || !room.CanStartGame || isStartingGame)
            return;

        isStartingGame = true;
        startGameError = null;

        int? gameId;
        try { gameId = await GameService.StartMultiplayerGameAsync(room.Code); }
        catch (GuessWord.Client.Infrastructure.Http.ApiRequestException error)
        {
            isStartingGame = false;
            startGameError = error.Message;
            return;
        }

        isStartingGame = false;

        if (!gameId.HasValue)
        {
            startGameError = "Не удалось запустить игру.";
            return;
        }

        Navigation.NavigateTo($"/game/multiplayer/{gameId.Value}");
    }

    private async Task LeaveRoomAsync()
    {
        if (room is null || isLeaving)
            return;

        isLeaving = true;

        var roomCode = room.Code;
        if (!await RoomService.LeaveRoomAsync(roomCode))
        {
            isLeaving = false;
            errorMessage = "Не удалось выйти из комнаты. Попробуйте ещё раз.";
            return;
        }

        if (hubConnection is not null)
        {
            try
            {
                await hubConnection.InvokeAsync("LeaveRoomGroup", roomCode);
                await hubConnection.StopAsync();
            }
            catch
            {
            }
        }

        isLeaving = false;
        Navigation.NavigateTo("/game");
    }

    private void NavigateToLobby()
    {
        Navigation.NavigateTo("/game");
    }

    private void NavigateToGameIfStarted()
    {
        if (room?.Status == RoomStatus.InGame && room.GameId.HasValue)
        {
            Navigation.NavigateTo($"/game/multiplayer/{room.GameId.Value}");
        }
    }

    private string GetRoomStatusText(RoomStatus status)
    {
        return status switch
        {
            RoomStatus.Waiting => room?.Players.Count >= 2 ? "Можно начинать" : "Ожидание участников",
            RoomStatus.Full => "Все пять мест заняты",
            RoomStatus.InGame => "Игра запущена",
            RoomStatus.Closed => "Комната закрыта",
            _ => "Неизвестный статус"
        };
    }

    public async ValueTask DisposeAsync()
    {
        disposed = true;
        if (subscription is not null) await subscription.DisposeAsync();
    }
}
