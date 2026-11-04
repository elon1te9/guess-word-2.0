using System.Net.Http;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using GuessWord.Contracts.Requests;
using GuessWord.Contracts.Responses;

namespace GuessWord.Client.Features.Leaderboard;

public partial class Leaderboard
{
    private LeaderboardResponseDto? _leaderboard;
    private bool _isLoading = true;
    private string? _errorMessage;

    private string TotalPlayersLabel => _leaderboard?.TotalPlayers.ToString() ?? "0";

    private string LeaderLabel => _leaderboard?.Entries.FirstOrDefault() is { } leader
        ? $"{leader.DisplayName} · {leader.WinsCount}"
        : "Пока нет данных";

    protected override async Task OnInitializedAsync()
    {
        UserState.OnChange += HandleUserStateChanged;
        await LoadLeaderboardAsync();
    }

    private async Task LoadLeaderboardAsync()
    {
        _errorMessage = null;
        _isLoading = true;
        StateHasChanged();

        var leaderboard = await UserService.GetLeaderboardAsync();

        _leaderboard = leaderboard;
        _errorMessage = leaderboard is null ? "Не удалось загрузить таблицу лидеров." : null;
        _isLoading = false;
    }

    private void HandleUserStateChanged()
    {
        _ = InvokeAsync(StateHasChanged);
    }

    private string GetRowClass(LeaderboardEntryResponseDto entry)
    {
        var classes = new List<string> { "leaderboard-row" };

        if (UserState.IsAuthorized && UserState.UserId == entry.UserId)
            classes.Add("leaderboard-row-current-user");

        return string.Join(" ", classes);
    }

    public void Dispose()
    {
        UserState.OnChange -= HandleUserStateChanged;
    }
}
