using GuessWord.Api.Data;
using GuessWord.Api.Models;
using GuessWord.Contracts.Enums;
using GuessWord.Contracts.Requests;
using GuessWord.Contracts.Responses;
using Microsoft.EntityFrameworkCore;

namespace GuessWord.Api.Features.Games;

public sealed class SingleGameCommands(AppDbContext db, GameDataAccess data, GameResponseBuilder responses, GameMutation mutation, GuessHandler guesses)
{
    public async Task<SingleGameResponseDto> StartSingleGameAsync(int userId)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        await mutation.LockUsersAsync([userId]);
        await mutation.RequireNoRoomAsync(userId);
        var active = await db.ActiveGameParticipations.Include(p => p.Player).ThenInclude(p => p.Game)
            .SingleOrDefaultAsync(p => p.UserId == userId);
        if (active is not null)
        {
            if (active.Player.Game.Mode != GameMode.Single)
                throw new GameRuleException("active_game", "У вас уже есть активная игра.");
            await tx.CommitAsync();
            return await responses.BuildGameResponseAsync(active.GameId, userId);
        }
        var secret = await data.GetRandomSecretWordAsync();
        var game = new Game { Mode = GameMode.Single, SecretWordId = secret.Id };
        var player = new GamePlayer { Game = game, UserId = userId, IsActiveSingleGame = true };
        db.ActiveGameParticipations.Add(new ActiveGameParticipation { UserId = userId, Player = player });
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return await responses.BuildGameResponseAsync(game.Id, userId);
    }

    public async Task<SingleGameResponseDto> SubmitGuessAsync(int userId, SubmitGuessRequestDto request)
    {
        var outcome = await guesses.SubmitAsync(userId, request, GameMode.Single);
        return await responses.BuildGameResponseAsync(request.GameId, userId, outcome.AttemptId, outcome.Repeated, outcome.Transient);
    }

    public async Task<SingleGameResponseDto> GiveUpSingleGameAsync(int userId, int gameId)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        var game = await mutation.LockGameAsync(gameId, userId, GameMode.Single);
        if (game.Status == GameStatus.InProgress)
        {
            GameMutation.RequirePlaying(game, userId).Result = GamePlayerResult.GaveUp;
            await mutation.FinishAsync(game, null);
            await db.SaveChangesAsync();
        }
        await tx.CommitAsync();
        return await responses.BuildGameResponseAsync(gameId, userId);
    }
}
