using GuessWord.Api.Data;
using GuessWord.Contracts.Enums;
using GuessWord.Contracts.Responses;
using Microsoft.EntityFrameworkCore;

namespace GuessWord.Api.Features.Games;

public class GameResponseBuilder
{
    private readonly AppDbContext _context;

    public GameResponseBuilder(AppDbContext context)
    {
        _context = context;
    }

    public async Task<GameStateDto?> GetGameStateAsync(int userId, int gameId)
    {
        var game = await _context.Games
            .AsNoTracking()
            .Include(g => g.SecretWord)
            .Include(g => g.Players)
                .ThenInclude(p => p.User)
            .Include(g => g.Attempts)
            .FirstOrDefaultAsync(g => g.Id == gameId);

        if (game is null || game.Mode != GameMode.Multiplayer || !game.Players.Any(p => p.UserId == userId)
            || game.Status == GameStatus.InProgress && !await _context.RoomMembers.AnyAsync(m => m.RoomId == game.RoomId && m.UserId == userId))
            return null;

        return new GameStateDto
        {
            RoomCode = await _context.Rooms.Where(r => r.Id == game.RoomId).Select(r => r.Code).SingleOrDefaultAsync(),
            GameId = game.Id,
            Status = game.Status,
            SecretWord = game.Status == GameStatus.Finished || game.Players.Any(p => p.UserId == userId && p.Result == GamePlayerResult.GaveUp) ? game.SecretWord.Text : null,
            SecretWordLength = game.SecretWord?.Text.Length,
            WinnerId = game.WinnerUserId,
            Players = BuildPlayers(game, userId)
        };
    }

    public async Task<SingleGameResponseDto> BuildGameResponseAsync(
        int gameId,
        int userId,
        int? lastAttemptId = null,
        bool lastAttemptWasRepeated = false,
        GameAttemptResponseDto? transientLastAttempt = null)
    {
        var game = await _context.Games
            .AsNoTracking()
            .Include(g => g.SecretWord)
            .Include(g => g.Attempts.Where(a => a.UserId == userId))
            .Include(g => g.Players.Where(p => p.UserId == userId))
            .FirstAsync(g => g.Id == gameId);

        var player = game.Players.First();
        var attempts = game.Attempts
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new GameAttemptResponseDto
            {
                Word = a.Word,
                Rank = a.Rank,
                IsValid = a.IsValid,
                IsRepeated = false,
                CreatedAt = a.CreatedAt
            })
            .ToList();

        GameAttemptResponseDto? lastAttempt = null;

        if (transientLastAttempt is not null)
        {
            lastAttempt = transientLastAttempt;
        }
        else if (lastAttemptId.HasValue)
        {
            var sourceAttempt = game.Attempts.FirstOrDefault(a => a.Id == lastAttemptId.Value);

            if (sourceAttempt is not null)
            {
                lastAttempt = new GameAttemptResponseDto
                {
                    Word = sourceAttempt.Word,
                    Rank = sourceAttempt.Rank,
                    IsValid = sourceAttempt.IsValid,
                    IsRepeated = lastAttemptWasRepeated,
                    CreatedAt = sourceAttempt.CreatedAt
                };
            }
        }

        return new SingleGameResponseDto
        {
            GameId = game.Id,
            AttemptsCount = player.AttemptsCount,
            Status = game.Status,
            PlayerResult = player.Result,
            SecretWord = game.Status == GameStatus.Finished ? game.SecretWord.Text : null,
            LastAttempt = lastAttempt,
            LastAttemptWasRepeated = lastAttemptWasRepeated,
            Attempts = attempts
        };
    }

    public async Task<MultiplayerGameResponseDto> BuildMultiplayerGameResponseAsync(
        int gameId,
        int userId,
        int? lastAttemptId = null,
        bool lastAttemptWasRepeated = false,
        GameAttemptResponseDto? transientLastAttempt = null)
    {
        var game = await _context.Games
            .AsNoTracking()
            .Include(g => g.SecretWord)
            .Include(g => g.Attempts)
            .Include(g => g.Players)
                .ThenInclude(p => p.User)
            .FirstAsync(g => g.Id == gameId);

        var player = game.Players.First(p => p.UserId == userId);

        var attempts = game.Attempts
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new GameAttemptResponseDto
            {
                Word = a.Word,
                Rank = a.Rank,
                IsValid = a.IsValid,
                IsRepeated = false,
                CreatedAt = a.CreatedAt
            })
            .ToList();

        GameAttemptResponseDto? lastAttempt = null;

        if (transientLastAttempt is not null)
        {
            lastAttempt = transientLastAttempt;
        }
        else if (lastAttemptId.HasValue)
        {
            var sourceAttempt = game.Attempts
                .FirstOrDefault(a => a.Id == lastAttemptId.Value && a.UserId == userId);

            if (sourceAttempt is not null)
            {
                lastAttempt = new GameAttemptResponseDto
                {
                    Word = sourceAttempt.Word,
                    Rank = sourceAttempt.Rank,
                    IsValid = sourceAttempt.IsValid,
                    IsRepeated = lastAttemptWasRepeated,
                    CreatedAt = sourceAttempt.CreatedAt
                };
            }
        }

        return new MultiplayerGameResponseDto
        {
            GameId = game.Id,
            Status = game.Status,
            PlayerResult = player.Result,
            SecretWord = game.Status == GameStatus.Finished || player.Result == GamePlayerResult.GaveUp ? game.SecretWord.Text : null,
            AttemptsCount = player.AttemptsCount,
            Attempts = attempts,
            LastAttempt = lastAttempt,
            LastAttemptWasRepeated = lastAttemptWasRepeated,
            Players = BuildPlayers(game, userId),
            IsWinner = game.WinnerUserId == userId
        };
    }
    private static List<GamePlayerStateDto> BuildPlayers(GuessWord.Api.Models.Game game, int userId) =>
        game.Players
                .OrderBy(p => p.UserId)
                .Select(player => new GamePlayerStateDto
                {
                    UserId = player.UserId,
                    Username = string.IsNullOrWhiteSpace(player.User.Name)
                        ? player.User.Login
                        : player.User.Name,
                    AttemptsCount = player.AttemptsCount,
                    BestRank = game.Attempts.Where(a => a.UserId == player.UserId && a.IsValid).Min(a => a.Rank),
                    Result = player.Result,
                    Attempts = game.Attempts
                        .Where(a => player.UserId == userId && a.UserId == userId)
                        .OrderByDescending(a => a.CreatedAt)
                        .Select(a => new GameAttemptDto
                        {
                            Word = a.Word,
                            Rank = a.Rank,
                            CreatedAt = a.CreatedAt
                        })
                        .ToList()
                })
                .ToList();

}
