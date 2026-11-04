using System.Net.Http;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using GuessWord.Contracts.Requests;
using GuessWord.Contracts.Responses;

namespace GuessWord.Client.Features.Home;

public partial class Home
{
    private bool IsAuthorized => UserState.IsAuthorized;

    private string PrimaryActionText => IsAuthorized ? "Одиночная игра" : "Зарегистрироваться";

    private string SecondaryActionText => IsAuthorized ? "Игра с друзьями" : "Войти";

    private string QuickStartText => IsAuthorized
        ? "Играйте самостоятельно или пригласите друзей в комнату на 2–5 игроков."
        : "Для игры нужен аккаунт.";

    protected override void OnInitialized()
    {
        UserState.OnChange += HandleUserStateChanged;
    }

    private void HandleUserStateChanged()
    {
        InvokeAsync(StateHasChanged);
    }

    private void NavigateToPrimaryAction()
    {
        NavigateTo(IsAuthorized ? "/game" : "/register");
    }

    private void NavigateToSecondaryAction()
    {
        NavigateTo(IsAuthorized ? "/game" : "/login");
    }

    private void NavigateToProfile()
    {
        NavigateTo(IsAuthorized ? "/profile" : "/login");
    }

    private void NavigateToHistory()
    {
        NavigateTo(IsAuthorized ? "/history" : "/login");
    }

    private void NavigateTo(string url)
    {
        Navigation.NavigateTo(url);
    }

    public void Dispose()
    {
        UserState.OnChange -= HandleUserStateChanged;
    }
}
