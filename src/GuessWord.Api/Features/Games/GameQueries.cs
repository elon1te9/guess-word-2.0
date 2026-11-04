using GuessWord.Api.Data;
using GuessWord.Contracts.Enums;
using GuessWord.Contracts.Responses;
using Microsoft.EntityFrameworkCore;

namespace GuessWord.Api.Features.Games;

public class GameQueries
{
    private readonly AppDbContext _context;
    private readonly GameDataAccess _gameDataAccess;
    private readonly GameResponseBuilder _gameResponseBuilder;

    public GameQueries(AppDbContext context, GameDataAccess gameDataAccess, GameResponseBuilder gameResponseBuilder)
    {
        _context = context;
        _gameDataAccess = gameDataAccess;
        _gameResponseBuilder = gameResponseBuilder;
    }

    public async Task<ActiveGameDto?> GetActiveGameAsync(int userId)
    {
        var activeGame = await _context.GamePlayers
            .AsNoTracking()
            .Where(gp => gp.UserId == userId && gp.Game.Status == GameStatus.InProgress && (gp.Result == GamePlayerResult.Playing || gp.Game.RoomId != null && _context.RoomMembers.Any(m => m.RoomId == gp.Game.RoomId && m.UserId == userId)))
            .Include(gp => gp.Game)
            .OrderByDescending(gp => gp.Game.CreatedAt)
            .Select(gp => new ActiveGameDto
            {
                GameId = gp.GameId,
                GameMode = gp.Game.Mode,
                Status = gp.Game.Status,
                RoomCode = gp.Game.Mode == GameMode.Multiplayer
                    ? _context.Rooms
                        .Where(r => r.GameId == gp.GameId)
                        .Select(r => r.Code)
                        .FirstOrDefault()
                    : null
            })
            .FirstOrDefaultAsync();

        return activeGame;
    }

    public async Task<SingleGameResponseDto?> GetCurrentSingleGameAsync(int userId)
    {
        var currentGame = await _gameDataAccess.GetCurrentGameEntityAsync(userId);
        if (currentGame is null)
            return null;

        return await _gameResponseBuilder.BuildGameResponseAsync(currentGame.Id, userId);
    }

    public async Task<MultiplayerGameResponseDto?> GetMultiplayerGameAsync(int userId, int gameId)
    {
        var game = await _context.Games
            .AsNoTracking()
            .Include(g => g.Players)
            .FirstOrDefaultAsync(g => g.Id == gameId);

        if (game is null || game.Mode != GameMode.Multiplayer || !game.Players.Any(p => p.UserId == userId)
            || game.Status == GameStatus.InProgress && !await _context.RoomMembers.AnyAsync(m => m.RoomId == game.RoomId && m.UserId == userId))
            return null;

        return await _gameResponseBuilder.BuildMultiplayerGameResponseAsync(gameId, userId);
    }
}
