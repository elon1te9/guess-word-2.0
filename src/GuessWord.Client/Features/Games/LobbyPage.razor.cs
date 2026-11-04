using System.Net.Http;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using GuessWord.Contracts.Requests;
using GuessWord.Contracts.Responses;
using GuessWord.Contracts.Enums;

namespace GuessWord.Client.Features.Games;

public partial class LobbyPage
{
    private bool disposed;
    private bool isLoading = true;
    private bool isActionLoading;
    private bool isCreatingRoom;
    private bool isJoiningRoom;
    private bool isJoinRoomModalOpen;
    private ActiveGameDto? activeGame;
    private string joinRoomCode = string.Empty;
    private string? roomError;
    private string? joinRoomModalError;
    private string errorMessage = string.Empty;

    private bool HasActiveGame => activeGame is not null;

    protected override async Task OnInitializedAsync()
    {
        UserState.OnChange += HandleUserStateChanged;
        await LoadLobbyStateAsync();
    }

    private async void HandleUserStateChanged()
    {
        try
        {
            await InvokeAsync(async () =>
            {
                if (disposed) return;
                await LoadLobbyStateAsync();
                if (!disposed) StateHasChanged();
            });
        }
        catch (Exception error) { await DispatchExceptionAsync(error); }
    }

    private async Task LoadLobbyStateAsync()
    {
        errorMessage = string.Empty;
        roomError = null;
        activeGame = null;

        if (!UserState.IsAuthorized)
        {
            isLoading = false;
            return;
        }

        isLoading = true;

        var currentRoom = await RoomService.GetCurrentRoomAsync();
        if (disposed) return;
        if (currentRoom is not null)
        {
            Navigation.NavigateTo($"/game/room/{currentRoom.Code}", replace: true);
            return;
        }

        activeGame = await GameService.GetActiveGameAsync();

        isLoading = false;
    }

    private void NavigateToLogin()
    {
        Navigation.NavigateTo("/login");
    }

    private Task ConnectToActiveGameAsync()
    {
        if (activeGame is null)
            return Task.CompletedTask;

        if (activeGame.GameMode == GameMode.Single)
        {
            Navigation.NavigateTo("/game/single");
            return Task.CompletedTask;
        }

        Navigation.NavigateTo($"/game/multiplayer/{activeGame.GameId}");
        return Task.CompletedTask;
    }

    private async Task StartSingleGameAsync()
    {
        if (isActionLoading || HasActiveGame)
            return;

        try
        {
            isActionLoading = true;
            errorMessage = string.Empty;

            var game = await GameService.StartSingleGameAsync();

            isActionLoading = false;

            if (game is null)
            {
                await LoadLobbyStateAsync();
                errorMessage = "Не удалось начать игру. Попробуйте ещё раз.";
                return;
            }

            await LoadLobbyStateAsync();
            Navigation.NavigateTo("/game/single");
        }
        catch (GuessWord.Client.Infrastructure.Http.ApiRequestException error)
        {
            errorMessage = error.Message;
        }
        finally
        {
            isActionLoading = false;
        }
    }

    private async Task CreateRoomAsync()
    {
        if (isCreatingRoom || HasActiveGame)
            return;

        isCreatingRoom = true;
        roomError = null;

        var room = await RoomService.CreateRoomAsync();

        isCreatingRoom = false;

        if (room is null)
        {
            await LoadLobbyStateAsync();
            roomError = "Не удалось создать комнату.";
            return;
        }

        Navigation.NavigateTo($"/game/room/{room.Code}");
    }

    private void OpenJoinRoomModal()
    {
        if (HasActiveGame)
            return;

        joinRoomModalError = null;
        joinRoomCode = string.Empty;
        isJoinRoomModalOpen = true;
    }

    private void CloseJoinRoomModal()
    {
        if (isJoiningRoom)
            return;

        isJoinRoomModalOpen = false;
        joinRoomModalError = null;
        joinRoomCode = string.Empty;
    }

    private async Task JoinRoomAsync()
    {
        if (isJoiningRoom || HasActiveGame)
            return;

        if (string.IsNullOrWhiteSpace(joinRoomCode))
        {
            joinRoomModalError = "Введите код комнаты.";
            return;
        }

        isJoiningRoom = true;
        joinRoomModalError = null;
        roomError = null;

        var room = await RoomService.JoinRoomAsync(joinRoomCode);

        isJoiningRoom = false;

        if (room is null)
        {
            await LoadLobbyStateAsync();
            joinRoomModalError = "Комната не найдена, уже заполнена или у тебя есть активная игра.";
            return;
        }

        CloseJoinRoomModal();
        Navigation.NavigateTo($"/game/room/{room.Code}");
    }

    public void Dispose()
    {
        disposed = true;
        UserState.OnChange -= HandleUserStateChanged;
    }
}
