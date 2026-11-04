using GuessWord.Api.Data;
using GuessWord.Contracts.Enums;
using GuessWord.Contracts.Responses;
using Microsoft.EntityFrameworkCore;

namespace GuessWord.Api.Features.Leaderboard;

public class LeaderboardService
{
    private readonly AppDbContext _context;

    public LeaderboardService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<LeaderboardResponseDto> GetLeaderboardAsync()
    {
        const int topLimit = 50;

        var leaderboardData = await _context.GamePlayers
            .AsNoTracking()
            .Where(gp => gp.Game.Status == GameStatus.Finished)
            .GroupBy(gp => new
            {
                gp.UserId,
                gp.User.Login,
                gp.User.Name
            })
            .Select(group => new
            {
                group.Key.UserId,
                DisplayName = string.IsNullOrWhiteSpace(group.Key.Name)
                    ? group.Key.Login
                    : group.Key.Name!,
                GamesCount = group.Count(),
                WinsCount = group.Count(gp => gp.Result == GamePlayerResult.Won)
            })
            .ToListAsync();

        var orderedEntries = leaderboardData
            .Select(entry => new
            {
                entry.UserId,
                entry.DisplayName,
                entry.GamesCount,
                entry.WinsCount,
                WinRate = entry.GamesCount == 0 ? 0 : entry.WinsCount * 100 / entry.GamesCount
            })
            .OrderByDescending(entry => entry.WinsCount)
            .ThenByDescending(entry => entry.WinRate)
            .ThenByDescending(entry => entry.GamesCount)
            .ThenBy(entry => entry.DisplayName)
            .Take(topLimit)
            .ToList();

        var entries = orderedEntries
            .Select((entry, index) => new LeaderboardEntryResponseDto
            {
                Place = index + 1,
                UserId = entry.UserId,
                DisplayName = entry.DisplayName,
                WinsCount = entry.WinsCount,
                GamesCount = entry.GamesCount,
                WinRate = entry.WinRate
            })
            .ToList();

        return new LeaderboardResponseDto
        {
            Entries = entries,
            TotalPlayers = leaderboardData.Count
        };
    }
}
