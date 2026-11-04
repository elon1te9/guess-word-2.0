using GuessWord.Api.Data;
using GuessWord.Api.Models;
using GuessWord.Contracts.Enums;
using Microsoft.EntityFrameworkCore;

namespace GuessWord.Api.Features.Games;

public class GameDataAccess
{
    private readonly AppDbContext _context;

    public GameDataAccess(AppDbContext context)
    {
        _context = context;
    }

    // Последний результат доступен после reload; активное участие проверяется отдельным запросом.
    public async Task<Game?> GetCurrentGameEntityAsync(int userId)
    {
        var gamePlayer = await _context.GamePlayers
            .Include(gp => gp.Game)
            .Where(gp =>
                gp.UserId == userId &&
                gp.Game.Mode == GameMode.Single)
            .OrderByDescending(gp => gp.Game.Status == GameStatus.InProgress)
            .ThenByDescending(gp => gp.Game.CreatedAt)
            .ThenByDescending(gp => gp.GameId)
            .FirstOrDefaultAsync();

        return gamePlayer?.Game;
    }

    public async Task<Word> GetRandomSecretWordAsync()
    {
        var secretWordsQuery = _context.Words
            .AsNoTracking()
            .Where(w => w.CanBeSecret && w.Embedding != null);

        var secretWordsCount = await secretWordsQuery.CountAsync();
        if (secretWordsCount == 0)
            throw new Exception("Секретные слова не найдены.");

        var randomIndex = Random.Shared.Next(secretWordsCount);

        return await secretWordsQuery
            .OrderBy(w => w.Id)
            .Skip(randomIndex)
            .FirstAsync();
    }

    public static string NormalizeRoomCode(string roomCode)
    {
        return roomCode.Trim().ToUpperInvariant();
    }

}
