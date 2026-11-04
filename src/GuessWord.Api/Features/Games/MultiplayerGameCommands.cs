using GuessWord.Api.Data;
using GuessWord.Api.Models;
using GuessWord.Contracts.Enums;
using GuessWord.Contracts.Requests;
using GuessWord.Contracts.Responses;
using Microsoft.EntityFrameworkCore;

namespace GuessWord.Api.Features.Games;

public sealed class MultiplayerGameCommands(AppDbContext db, GameDataAccess data, GameResponseBuilder responses, GameMutation mutation, GuessHandler guesses)
{
    public async Task<int?> StartMultiplayerGameAsync(int userId, string roomCode)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        var code = GameDataAccess.NormalizeRoomCode(roomCode);
        var room = await db.Rooms.FromSqlInterpolated($"SELECT * FROM \"Rooms\" WHERE \"Code\" = {code} FOR UPDATE").SingleOrDefaultAsync();
        if (room is null || room.HostUserId != userId)
            throw new GameRuleException("room_not_ready", "Комната не готова к запуску.");
        if (room.Status == RoomStatus.InGame && room.GameId.HasValue) return room.GameId;
        var users = await db.RoomMembers.Where(m => m.RoomId == room.Id).Select(m => m.UserId).ToArrayAsync();
        if (users.Length is < 2 or > 5 || room.Status is not (RoomStatus.Waiting or RoomStatus.Full))
            throw new GameRuleException("room_not_ready", "Комната не готова к запуску.");
        await mutation.LockUsersAsync(users);
        if (await db.ActiveGameParticipations.AnyAsync(p => users.Contains(p.UserId)))
            throw new GameRuleException("active_game", "У участника уже есть активная игра.");
        var secret = await data.GetRandomSecretWordAsync();
        var game = new Game { Mode = GameMode.Multiplayer, SecretWordId = secret.Id, RoomId = room.Id };
        foreach (var id in users)
            db.ActiveGameParticipations.Add(new ActiveGameParticipation { UserId = id, Player = new GamePlayer { UserId = id, Game = game } });
        await db.SaveChangesAsync();
        room.GameId = game.Id;
        room.Status = RoomStatus.InGame;
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return game.Id;
    }

    public async Task<MultiplayerGameResponseDto?> SubmitMultiplayerGuessAsync(int userId, SubmitGuessRequestDto request)
    {
        var outcome = await guesses.SubmitAsync(userId, request, GameMode.Multiplayer);
        return await responses.BuildMultiplayerGameResponseAsync(request.GameId, userId, outcome.AttemptId, outcome.Repeated, outcome.Transient);
    }

    public async Task<GameStateDto?> GiveUpMultiplayerGameAsync(int userId, int gameId)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        var game = await mutation.LockGameAsync(gameId, userId, GameMode.Multiplayer);
        var player = game.Players.Single(p => p.UserId == userId);
        if (game.Status == GameStatus.InProgress && player.Result == GamePlayerResult.Playing)
        {
            player.Result = GamePlayerResult.GaveUp;
            var remaining = game.Players.Where(p => p.Result == GamePlayerResult.Playing).ToList();
            if (remaining.Count <= 1) await mutation.FinishAsync(game, remaining.SingleOrDefault()?.UserId);
            await db.SaveChangesAsync();
        }
        await tx.CommitAsync();
        return await responses.GetGameStateAsync(userId, gameId);
    }
}
