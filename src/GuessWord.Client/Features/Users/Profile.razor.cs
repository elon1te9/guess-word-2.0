using System.Net.Http;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using GuessWord.Contracts.Requests;
using GuessWord.Contracts.Responses;

namespace GuessWord.Client.Features.Users;

public partial class Profile
{
    private GuessWord.Contracts.Responses.UserProfileResponseDto? _profile;
    private bool _isLoading;
    private string? _errorMessage;

    private string DisplayName =>
        !string.IsNullOrWhiteSpace(_profile?.Name) ? _profile.Name :
        !string.IsNullOrWhiteSpace(UserState.Name) ? UserState.Name! :
        !string.IsNullOrWhiteSpace(_profile?.Login) ? _profile.Login :
        !string.IsNullOrWhiteSpace(UserState.Login) ? UserState.Login! :
        "Пользователь";

    protected override async Task OnInitializedAsync()
    {
        UserState.OnChange += HandleUserStateChanged;
        await UserState.WaitForInitializationAsync();
        await LoadProfileAsync();
    }

    private async Task LoadProfileAsync()
    {
        _profile = null;
        _errorMessage = null;

        if (!UserState.IsInitialized)
        {
            _isLoading = true;
            return;
        }

        if (!UserState.IsAuthorized)
        {
            _isLoading = false;
            return;
        }

        _isLoading = true;
        StateHasChanged();

        _profile = await UserService.GetProfileAsync();
        _errorMessage = _profile is null ? "Не удалось загрузить профиль." : null;
        _isLoading = false;
    }

    private void HandleUserStateChanged()
    {
        _ = InvokeAsync(LoadProfileAsync);
    }

    private void NavigateToLogin()
    {
        Navigation.NavigateTo("/login");
    }

    private void NavigateToHistory()
    {
        Navigation.NavigateTo("/history");
    }

    private async Task Logout()
    {
        await AuthService.Logout();
        Navigation.NavigateTo("/login");
    }

    public void Dispose()
    {
        UserState.OnChange -= HandleUserStateChanged;
    }
}
