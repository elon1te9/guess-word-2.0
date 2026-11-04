using GuessWord.Api.Data;
using GuessWord.Api.Features.Dictionary;
using GuessWord.Api.Models;
using GuessWord.Contracts.Enums;
using GuessWord.Contracts.Requests;
using GuessWord.Contracts.Responses;
using Microsoft.EntityFrameworkCore;

namespace GuessWord.Api.Features.Games;

public sealed record GuessOutcome(int? AttemptId = null, bool Repeated = false, GameAttemptResponseDto? Transient = null);

public sealed class GuessHandler(AppDbContext db, GameMutation mutation, IRankService rankService)
{
    public async Task<GuessOutcome> SubmitAsync(int userId, SubmitGuessRequestDto request, GameMode mode)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        var game = await mutation.LockGameAsync(request.GameId, userId, mode);
        var player = GameMutation.RequirePlaying(game, userId);
        var normalized = WordNormalizer.Normalize(request.Word);
        if (normalized.Length == 0)
            throw new GameRuleException("empty_word", "Введите слово.", 400);

        var existing = await db.GameAttempts.AsNoTracking().FirstOrDefaultAsync(a =>
            a.GameId == game.Id && a.UserId == userId && a.Word == normalized && a.IsValid);
        if (existing is not null)
        {
            await tx.CommitAsync();
            return new(existing.Id, true);
        }
        var word = await db.Words.AsNoTracking().SingleOrDefaultAsync(w => w.Text == normalized);
        if (word is null)
        {
            await tx.CommitAsync();
            return new(Transient: new GameAttemptResponseDto { Word = normalized, IsValid = false, CreatedAt = DateTime.UtcNow });
        }
        var attempt = new GameAttempt
        {
            GameId = game.Id,
            UserId = userId,
            Word = normalized,
            IsValid = true,
            Rank = await rankService.GetRankAsync(game.SecretWordId, word.Id)
        };
        db.GameAttempts.Add(attempt);
        player.AttemptsCount++;
        if (word.Id == game.SecretWordId) await mutation.FinishAsync(game, userId);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return new(attempt.Id);
    }
}
