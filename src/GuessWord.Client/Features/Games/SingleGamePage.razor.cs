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

namespace GuessWord.Client.Features.Games;

public partial class SingleGamePage
{
    private bool resultDismissed;
    private bool isLoading = true;
    private bool isSubmitting;
    private bool isStartingGame;
    private bool showNotFoundCard;
    private bool shouldFocusGuessInput;
    private string guessWord = string.Empty;
    private string pageMessage = string.Empty;
    private ElementReference guessInputRef;
    private SingleGameResponseDto? currentGame;
    private GameAttemptResponseDto? lastSubmittedAttempt;
    private List<GameAttemptResponseDto> displayAttempts = new();

    protected override async Task OnInitializedAsync()
    {
        UserState.OnChange += HandleUserStateChanged;
        await LoadCurrentGameAsync();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!shouldFocusGuessInput)
            return;

        shouldFocusGuessInput = false;

        if (currentGame is null || currentGame.Status == GameStatus.Finished || isSubmitting)
            return;

        await guessInputRef.FocusAsync();
    }

    private void HandleUserStateChanged()
    {
        _ = InvokeAsync(async () =>
        {
            await LoadCurrentGameAsync();
            StateHasChanged();
        });
    }

    private async Task LoadCurrentGameAsync()
    {
        pageMessage = string.Empty;
        guessWord = string.Empty;
        lastSubmittedAttempt = null;
        showNotFoundCard = false;

        if (!UserState.IsAuthorized)
        {
            currentGame = null;
            displayAttempts = new();
            isLoading = false;
            return;
        }

        isLoading = true;
        currentGame = await GameService.GetCurrentSingleGameAsync();
        UpdateDisplayAttempts();
        isLoading = false;
    }

    private void NavigateToLogin()
    {
        Navigation.NavigateTo("/login");
    }

    private void NavigateToLobby()
    {
        Navigation.NavigateTo("/game");
    }

    private async Task StartNewGameAsync()
    {
        if (isStartingGame || !UserState.IsAuthorized)
            return;

        try
        {
            isStartingGame = true;
            pageMessage = string.Empty;

            var startedGame = await GameService.StartSingleGameAsync();

            isStartingGame = false;

            if (startedGame is null)
            {
                pageMessage = "Не удалось начать игру. Попробуйте ещё раз.";
                return;
            }

            resultDismissed = false;
            currentGame = startedGame;
            UpdateDisplayAttempts();
            lastSubmittedAttempt = null;
            showNotFoundCard = false;
            guessWord = string.Empty;
        }
        catch (GuessWord.Client.Infrastructure.Http.ApiRequestException error)
        {
            pageMessage = error.Message;
        }
        finally
        {
            isStartingGame = false;
        }
    }

    private async Task SubmitGuessAsync()
    {
        if (currentGame is null || isSubmitting || currentGame.Status == GameStatus.Finished)
            return;

        try
        {
            var trimmedWord = guessWord.Trim();

            if (string.IsNullOrWhiteSpace(trimmedWord))
            {
                pageMessage = string.Empty;
                return;
            }

            isSubmitting = true;
            pageMessage = string.Empty;

            var updatedGame = await GameService.SubmitGuessAsync(currentGame.GameId, trimmedWord);

            isSubmitting = false;

            if (updatedGame is null)
            {
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

            currentGame = updatedGame;
            UpdateDisplayAttempts();
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
        if (currentGame is null || isSubmitting || currentGame.Status == GameStatus.Finished)
            return;

        try
        {
            isSubmitting = true;
            pageMessage = string.Empty;

            var updatedGame = await GameService.GiveUpSingleGameAsync(currentGame.GameId);

            isSubmitting = false;

            if (updatedGame is null)
            {
                pageMessage = "Не удалось завершить игру.";
                return;
            }

            currentGame = updatedGame;
            UpdateDisplayAttempts();
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
        return GameAttemptDisplayHelper.BuildSingleGameAttempts(currentGame?.Attempts);
    }

    private static string GetAttemptKey(GameAttemptResponseDto attempt)
    {
        return GameAttemptDisplayHelper.BuildAttemptKey(attempt);
    }

    public void Dispose()
    {
        UserState.OnChange -= HandleUserStateChanged;
    }
}
