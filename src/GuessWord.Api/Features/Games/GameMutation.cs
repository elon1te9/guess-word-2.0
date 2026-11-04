using GuessWord.Api.Data;
using GuessWord.Api.Models;
using GuessWord.Contracts.Enums;
using Microsoft.EntityFrameworkCore;

namespace GuessWord.Api.Features.Games;

public sealed class GameMutation(AppDbContext db)
{
    // Вызывается внутри транзакции, до чтения отслеживаемых сущностей.
    // Все команды соблюдают Room → Game → Users (по возрастанию ID).
    public async Task<Game> LockGameAsync(int gameId, int userId, GameMode mode)
    {
        await db.Rooms.FromSqlInterpolated($"SELECT * FROM \"Rooms\" WHERE \"GameId\" = {gameId} ORDER BY \"Id\" FOR UPDATE").ToListAsync();
        var game = await db.Games.FromSqlInterpolated($"SELECT * FROM \"Games\" WHERE \"Id\" = {gameId} FOR UPDATE").SingleOrDefaultAsync();
        if (game is null || game.Mode != mode)
            throw new GameRuleException("game_not_found", "Игра не найдена.", 404);
        await db.Entry(game).Collection(g => g.Players).LoadAsync();
        if (!game.Players.Any(p => p.UserId == userId))
            throw new GameRuleException("not_participant", "Вы не участник этой игры.", 403);
        if (mode == GameMode.Multiplayer && game.Status == GameStatus.InProgress
            && !await db.RoomMembers.AnyAsync(m => m.RoomId == game.RoomId && m.UserId == userId))
            throw new GameRuleException("not_participant", "Вы вышли из комнаты.", 403);
        await LockUsersAsync(game.Players.Select(p => p.UserId));
        return game;
    }

    public async Task LockUsersAsync(IEnumerable<int> userIds)
    {
        foreach (var id in userIds.Distinct().Order())
            await db.Users.FromSqlInterpolated($"SELECT * FROM \"Users\" WHERE \"Id\" = {id} FOR UPDATE").ToListAsync();
    }

    // Проверяется после блокировки пользователя: Create/Join/Single не обходят друг друга.
    public async Task RequireNoRoomAsync(int userId)
    {
        if (await db.RoomMembers.AnyAsync(m => m.UserId == userId && m.Room.Status != RoomStatus.Closed))
            throw new GameRuleException("already_in_room", "Сначала выйдите из текущей комнаты, чтобы начать новую игру или присоединиться к другой.");
    }

    public static GamePlayer RequirePlaying(Game game, int userId)
    {
        if (game.Status != GameStatus.InProgress)
            throw new GameRuleException("game_finished", "Игра уже завершена.");
        var player = game.Players.Single(p => p.UserId == userId);
        if (player.Result != GamePlayerResult.Playing)
            throw new GameRuleException("player_finished", "Вы уже завершили участие.");
        return player;
    }

    public async Task FinishAsync(Game game, int? winner)
    {
        game.Status = GameStatus.Finished;
        game.WinnerUserId = winner;
        game.FinishedAt = DateTime.UtcNow;
        foreach (var p in game.Players)
        {
            p.IsActiveSingleGame = false;
            if (p.Result == GamePlayerResult.Playing)
                p.Result = p.UserId == winner ? GamePlayerResult.Won : GamePlayerResult.Lost;
        }
        var active = await db.ActiveGameParticipations.Where(p => p.GameId == game.Id).ToListAsync();
        db.ActiveGameParticipations.RemoveRange(active);
        var rooms = await db.Rooms.Where(r => r.GameId == game.Id).ToListAsync();
        foreach (var room in rooms)
        {
            var count = await db.RoomMembers.CountAsync(m => m.RoomId == room.Id);
            room.Status = count == 0 ? RoomStatus.Closed : count == 5 ? RoomStatus.Full : RoomStatus.Waiting;
        }
    }
}
