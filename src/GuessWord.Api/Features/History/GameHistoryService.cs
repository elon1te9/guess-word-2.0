using GuessWord.Api.Data;
using GuessWord.Contracts.Enums;
using GuessWord.Contracts.Responses;
using Microsoft.EntityFrameworkCore;

namespace GuessWord.Api.Features.History;

public class GameHistoryService
{
    private readonly AppDbContext _context;

    public GameHistoryService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<GameHistoryItemResponseDto>> GetHistoryAsync(int userId)
    {
        var gamePlayers = await _context.GamePlayers
            .Where(gp => gp.UserId == userId && gp.Game.Status == GameStatus.Finished)
            .Include(gp => gp.Game)
                .ThenInclude(g => g.SecretWord)
            .Include(gp => gp.Game)
                .ThenInclude(g => g.Players)
                    .ThenInclude(p => p.User)
            .OrderByDescending(gp => gp.Game.FinishedAt ?? gp.Game.CreatedAt)
            .ToListAsync();

        return gamePlayers
            .Select(gp =>
            {
                var game = gp.Game;
                var opponents = game.Players.Where(p => p.UserId != userId).OrderBy(p => p.UserId)
                    .Select(p => string.IsNullOrWhiteSpace(p.User.Name) ? p.User.Login : p.User.Name!).ToList();

                var result = gp.Result switch
                {
                    GamePlayerResult.Won => "Победа",
                    GamePlayerResult.GaveUp => "Сдался",
                    GamePlayerResult.Lost => "Поражение",
                    _ when game.WinnerUserId.HasValue && game.WinnerUserId.Value != userId => "Поражение",
                    _ => "Завершена"
                };

                return new GameHistoryItemResponseDto
                {
                    Date = game.FinishedAt ?? game.CreatedAt,
                    GameType = game.Mode == GameMode.Single ? "Сингл" : "Мультиплеер",
                    Result = result,
                    AttemptsCount = gp.AttemptsCount,
                    SecretWord = game.SecretWord?.Text ?? "-",
                    OpponentNames = opponents
                };
            })
            .ToList();
    }
}
