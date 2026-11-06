using System.Net.Http;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using GuessWord.Contracts.Requests;
using GuessWord.Contracts.Responses;
using GuessWord.Client.Components.Game;
using GuessWord.Client.Features.Games;
using GuessWord.Contracts.Enums;
using Microsoft.AspNetCore.SignalR.Client;

namespace GuessWord.Client.Features.Games;

public partial class MultiplayerGamePage
{
    [Parameter]
    public int GameId { get; set; }

    private ElementReference resultDialogRef;
    private IJSObjectReference? resultDialogModule;
    private int? shownResultGameId;
    private bool isLoading = true;
    private bool isSubmitting;
    private bool showNotFoundCard;
    private bool shouldFocusGuessInput;
    private string guessWord = string.Empty;
    private string pageMessage = string.Empty;
    private string? signalRErrorMessage;
    private ElementReference guessInputRef;
    private GameStateDto? currentGame;
    private GameAttemptResponseDto? lastSubmittedAttempt;
    private List<GameAttemptResponseDto> displayAttempts = new();
    private HubConnection? hubConnection;
    private Infrastructure.Realtime.GameHubSubscription? subscription;
    private bool disposed;
    private int snapshotRequest;
    private int? loadedRoute;

    protected override async Task OnParametersSetAsync()
    {
        if (loadedRoute == GameId) return;
        loadedRoute = GameId;
        ++snapshotRequest;
        currentGame = null;
        shownResultGameId = null;
        displayAttempts.Clear();
        if (subscription is not null) await subscription.DisposeAsync();
        subscription = null;
        isLoading = true;
        if (!UserState.IsAuthorized)
        {
            Navigation.NavigateTo("/login");
            return;
        }

        await LoadGameState(GameId);

        if (!disposed) await InitializeSignalRAsync();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (HasPersonalResult && currentGame is not null && shownResultGameId != currentGame.GameId)
        {
            shownResultGameId = currentGame.GameId;
            await ShowResultAsync();
        }

        if (!shouldFocusGuessInput)
            return;

        shouldFocusGuessInput = false;

        if (currentGame is null || !CanPlay || isSubmitting)
            return;

        await guessInputRef.FocusAsync();
    }

    private async Task ShowResultAsync()
    {
        resultDialogModule ??= await JS.InvokeAsync<IJSObjectReference>("import", "./js/match-result-dialog.js");
        await resultDialogModule.InvokeVoidAsync("show", resultDialogRef, null, "#match-game-heading");
    }

    private async Task LoadGameState(int gameId, bool resetTransientState = true, CancellationToken cancellationToken = default)
    {
        if (resetTransientState)
        {
            pageMessage = string.Empty;
            guessWord = string.Empty;
            lastSubmittedAttempt = null;
            showNotFoundCard = false;
        }

        var request = ++snapshotRequest;
        var snapshot = await GameService.GetGameStateAsync(gameId, cancellationToken);
        if (disposed || request != snapshotRequest) return;
        currentGame = snapshot;
        UpdateDisplayAttempts();
        isLoading = false;

        if (currentGame is null)
            pageMessage = "Не удалось получить состояние игры.";
    }

    private async Task InitializeSignalRAsync()
    {
        hubConnection = HubConnections.Create();

        hubConnection.On<int>("GameUpdated", async updatedGameId =>
        {
            if (updatedGameId == GameId)
                await ReloadGameStateAsync();
        });

        subscription = new(hubConnection, async token =>
        {
            await hubConnection.InvokeAsync("JoinGameGroup", GameId, token);
            await ReloadGameStateAsync(token);
            if (currentGame is null) throw new HttpRequestException("Снимок матча недоступен.");
        }, connected => InvokeAsync(() =>
        {
            if (disposed) return;
            signalRErrorMessage = connected ? null : "Связь потеряна. Повторное подключение…";
            StateHasChanged();
        }));
        subscription.Start();
        await Task.CompletedTask;
    }

    private async Task ReloadGameStateAsync(CancellationToken cancellationToken = default)
    {
        await LoadGameState(GameId, resetTransientState: false, cancellationToken);
        if (!disposed) await InvokeAsync(StateHasChanged);
    }

    private void NavigateToLogin()
    {
        Navigation.NavigateTo("/login");
    }

    private async Task NavigateToLobby()
    {
        if (isSubmitting) return;
        isSubmitting = true;
        try { await NavigateToCurrentRoomAsync(); }
        finally { isSubmitting = false; }
    }

    private async Task NavigateToCurrentRoomAsync()
    {
        var room = currentGame?.RoomCode is string code
            ? await RoomService.GetRoomAsync(code)
            : await RoomService.GetCurrentRoomAsync();
        if (disposed) return;
        if (room is null && currentGame is not null)
        {
            pageMessage = "Не удалось получить состояние комнаты. Попробуйте ещё раз.";
            return;
        }
        if (resultDialogModule is not null && currentGame is not null && HasPersonalResult)
            await resultDialogModule.InvokeVoidAsync("close", resultDialogRef);
        // Страница комнаты повторно читает снимок и открывает актуальный раунд,
        // даже если хост успел начать его во время перехода.
        Navigation.NavigateTo(room is null ? "/game" : $"/game/room/{room.Code}");
    }

    private async Task LeaveRoomAsync()
    {
        if (isSubmitting || currentGame?.RoomCode is not string code) return;
        isSubmitting = true;
        try
        {
            if (await RoomService.LeaveRoomAsync(code, currentGame.GameId)) Navigation.NavigateTo("/game");
            else pageMessage = "Не удалось выйти из комнаты.";
        }
        catch (Infrastructure.Http.ApiRequestException error) when (error.Code == "room_round_changed")
        {
            await NavigateToCurrentRoomAsync();
        }
        catch (Infrastructure.Http.ApiRequestException error)
        {
            pageMessage = error.Message;
            await ReloadGameStateAsync();
        }
        finally { isSubmitting = false; }
    }

    private async Task SubmitGuessAsync()
    {
        if (currentGame is null || isSubmitting || !CanPlay)
            return;

        try
        {
            var trimmedWord = guessWord.Trim();

            if (string.IsNullOrWhiteSpace(trimmedWord))
            {
                pageMessage = "Введите слово.";
                return;
            }

            isSubmitting = true;
            pageMessage = string.Empty;

            var updatedGame = await GameService.SubmitMultiplayerGuessAsync(GameId, trimmedWord);

            isSubmitting = false;

            if (updatedGame is null)
            {
                await ReloadGameStateAsync();
                pageMessage = "Не удалось отправить слово.";
                shouldFocusGuessInput = true;
                return;
            }

            var latestAttempt = updatedGame.LastAttempt;

            if (latestAttempt is null)
            {
                lastSubmittedAttempt = null;
                showNotFoundCard = false;
            }
            else if (!latestAttempt.IsValid)
            {
                lastSubmittedAttempt = null;
                showNotFoundCard = true;
                pageMessage = string.Empty;
            }
            else if (updatedGame.LastAttemptWasRepeated)
            {
                lastSubmittedAttempt = latestAttempt;
                showNotFoundCard = false;
                pageMessage = "Это слово уже было введено.";
            }
            else
            {
                lastSubmittedAttempt = latestAttempt;
                showNotFoundCard = false;
                pageMessage = string.Empty;
            }

            await LoadGameState(GameId, resetTransientState: false);
            guessWord = string.Empty;
            shouldFocusGuessInput = true;
        }
        catch (GuessWord.Client.Infrastructure.Http.ApiRequestException error)
        {
            pageMessage = error.Message;
        }
        finally
        {
            isSubmitting = false;
        }
    }

    private async Task GiveUpAsync()
    {
        if (currentGame is null || isSubmitting || !CanPlay)
            return;

        try
        {
            isSubmitting = true;
            pageMessage = string.Empty;

            var updatedGame = await GameService.GiveUpMultiplayerGameAsync(GameId);

            isSubmitting = false;

            if (updatedGame is null)
            {
                await ReloadGameStateAsync();
                pageMessage = "Не удалось завершить игру.";
                return;
            }

            await ReloadGameStateAsync();
            await InvokeAsync(StateHasChanged);
        }
        catch (GuessWord.Client.Infrastructure.Http.ApiRequestException error)
        {
            pageMessage = error.Message;
        }
        finally
        {
            isSubmitting = false;
        }
    }

    private void UpdateDisplayAttempts()
    {
        displayAttempts = BuildDisplayAttempts();
    }

    private List<GameAttemptResponseDto> BuildDisplayAttempts()
    {
        return GameAttemptDisplayHelper.BuildMultiplayerAttempts(GetCurrentPlayer()?.Attempts);
    }

    private GamePlayerStateDto? GetCurrentPlayer()
    {
        return currentGame?.Players.FirstOrDefault(player => player.UserId == UserState.UserId);
    }

    private static string GetAttemptKey(GameAttemptResponseDto attempt)
    {
        return GameAttemptDisplayHelper.BuildAttemptKey(attempt);
    }

    private int GetAttemptsCount() => GetCurrentPlayer()?.AttemptsCount ?? 0;
    private bool HasPersonalResult => currentGame?.Status == GameStatus.Finished || GetCurrentPlayer()?.Result == GamePlayerResult.GaveUp;
    private string GetPlayerCountText() => currentGame?.Players.Count switch
    {
        1 => "1 игрок", 2 => "2 игрока", 3 => "3 игрока", 4 => "4 игрока",
        var count => $"{count ?? 0} игроков"
    };
    private bool CanPlay => currentGame?.Status == GameStatus.InProgress && GetCurrentPlayer()?.Result == GamePlayerResult.Playing;
    private static string GetPlayerStatus(GamePlayerResult result) => result switch
    {
        GamePlayerResult.Playing => "Играет", GamePlayerResult.GaveUp => "Сдался",
        GamePlayerResult.Won => "Победа", _ => "Поражение"
    };

    private bool IsCurrentUserWinner()
    {
        return currentGame?.WinnerId == UserState.UserId;
    }

    private string GetResultValue()
    {
        if (currentGame is null)
            return "Завершена";

        if (GetCurrentPlayer()?.Result == GamePlayerResult.GaveUp) return "Поражение";
        if (currentGame.WinnerId is null) return "Без победителя";
        return IsCurrentUserWinner()
            ? "Победа"
            : "Поражение";
    }

    public async ValueTask DisposeAsync()
    {
        disposed = true;
        if (subscription is not null) await subscription.DisposeAsync();
        if (resultDialogModule is not null) await resultDialogModule.DisposeAsync();
    }
}
