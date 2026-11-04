using System.Net.Http;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using GuessWord.Contracts.Requests;
using GuessWord.Contracts.Responses;

namespace GuessWord.Client.Features.History;

public partial class History
{
    private MatchFilter _selectedFilter = MatchFilter.All;
    private List<GuessWord.Contracts.Responses.GameHistoryItemResponseDto> _matches = [];
    private bool _isLoading;
    private string? _errorMessage;

    private List<GuessWord.Contracts.Responses.GameHistoryItemResponseDto> FilteredMatches => _matches
        .Where(match => _selectedFilter == MatchFilter.All || MapFilter(match.GameType) == _selectedFilter)
        .ToList();

    private string FilterLabel => _selectedFilter switch
    {
        MatchFilter.Single => "сингл",
        MatchFilter.Multiplayer => "мультиплеер",
        _ => "все"
    };

    protected override async Task OnInitializedAsync()
    {
        UserState.OnChange += HandleUserStateChanged;
        await UserState.WaitForInitializationAsync();
        await LoadHistoryAsync();
    }

    private async Task LoadHistoryAsync()
    {
        _matches = [];
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

        var history = await GameService.GetHistoryAsync();
        _matches = history ?? [];
        _errorMessage = history is null ? "Не удалось загрузить историю матчей." : null;
        _isLoading = false;
    }

    private void HandleUserStateChanged()
    {
        _ = InvokeAsync(LoadHistoryAsync);
    }

    private void NavigateToLogin()
    {
        Navigation.NavigateTo("/login");
    }

    private void CycleFilter()
    {
        _selectedFilter = _selectedFilter switch
        {
            MatchFilter.All => MatchFilter.Single,
            MatchFilter.Single => MatchFilter.Multiplayer,
            _ => MatchFilter.All
        };
    }

    private static MatchFilter MapFilter(string gameType)
    {
        return gameType switch
        {
            "Сингл" => MatchFilter.Single,
            "Мультиплеер" => MatchFilter.Multiplayer,
            _ => MatchFilter.All
        };
    }

    public void Dispose()
    {
        UserState.OnChange -= HandleUserStateChanged;
    }

    private enum MatchFilter
    {
        All,
        Single,
        Multiplayer
    }
}
