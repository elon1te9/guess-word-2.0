using GuessWord.Api.Data;
using GuessWord.Api.Features.Games;
using GuessWord.Api.Models;
using GuessWord.Contracts.Enums;
using GuessWord.Contracts.Responses;
using Microsoft.EntityFrameworkCore;

namespace GuessWord.Api.Features.Rooms;

public class RoomService(AppDbContext db, GameMutation mutation) : IRoomService
{
    public async Task<RoomResponseDto?> CreateRoomAsync(int userId)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        await mutation.LockUsersAsync([userId]);
        await mutation.RequireNoRoomAsync(userId);
        if (await db.ActiveGameParticipations.AnyAsync(p => p.UserId == userId)) return null;
        string code;
        do { code = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(3)); }
        while (await db.Rooms.AnyAsync(r => r.Code == code));
        var room = new Room { Code = code, HostUserId = userId, Members = [new RoomMember { UserId = userId }] };
        db.Rooms.Add(room);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return await GetRoomAsync(userId, code);
    }

    public async Task<RoomResponseDto?> JoinRoomAsync(int userId, string code)
    {
        code = GameDataAccess.NormalizeRoomCode(code);
        await using var tx = await db.Database.BeginTransactionAsync();
        var room = await db.Rooms.FromSqlInterpolated($"SELECT * FROM \"Rooms\" WHERE \"Code\" = {code} FOR UPDATE").SingleOrDefaultAsync();
        if (room is null || room.Status == RoomStatus.Closed) return null;
        await db.Entry(room).Collection(r => r.Members).LoadAsync();
        if (!room.Members.Any(m => m.UserId == userId))
        {
            if (room.Status == RoomStatus.InGame || room.Members.Count >= 5) return null;
            await mutation.LockUsersAsync([userId]);
            await mutation.RequireNoRoomAsync(userId);
            if (await db.ActiveGameParticipations.AnyAsync(p => p.UserId == userId)) return null;
            room.Members.Add(new RoomMember { UserId = userId });
            room.Status = room.Members.Count == 5 ? RoomStatus.Full : RoomStatus.Waiting;
            await db.SaveChangesAsync();
        }
        await tx.CommitAsync();
        return await GetRoomAsync(userId, code);
    }

    public async Task<RoomResponseDto?> GetCurrentRoomAsync(int userId)
    {
        var code = await db.RoomMembers.AsNoTracking()
            .Where(m => m.UserId == userId && m.Room.Status != RoomStatus.Closed)
            .OrderBy(m => m.JoinedAt).ThenBy(m => m.Id)
            .Select(m => m.Room.Code).FirstOrDefaultAsync();
        return code is null ? null : await GetRoomAsync(userId, code);
    }

    public async Task<RoomResponseDto?> GetRoomAsync(int userId, string code)
    {
        code = GameDataAccess.NormalizeRoomCode(code);
        var room = await db.Rooms.AsNoTracking().Include(r => r.Members).ThenInclude(m => m.User)
            .SingleOrDefaultAsync(r => r.Code == code && r.Members.Any(m => m.UserId == userId));
        if (room is null || room.Status == RoomStatus.Closed) return null;
        return new RoomResponseDto
        {
            Code = room.Code, Status = room.Status, GameId = room.GameId,
            Players = room.Members.OrderBy(m => m.JoinedAt).ThenBy(m => m.Id)
                .Select(m => new RoomMemberDto(m.UserId, m.User.Name ?? m.User.Login, m.UserId == room.HostUserId)).ToList(),
            IsFull = room.Members.Count >= 5, IsHost = room.HostUserId == userId,
            CanStartGame = room.HostUserId == userId && room.Members.Count >= 2 && room.Status is RoomStatus.Waiting or RoomStatus.Full
        };
    }

    public async Task<bool> LeaveRoomAsync(int userId, string code, int? expectedGameId = null)
    {
        code = GameDataAccess.NormalizeRoomCode(code);
        await using var tx = await db.Database.BeginTransactionAsync();
        var room = await db.Rooms.FromSqlInterpolated($"SELECT * FROM \"Rooms\" WHERE \"Code\" = {code} FOR UPDATE").SingleOrDefaultAsync();
        if (room is null) return false;
        await db.Entry(room).Collection(r => r.Members).LoadAsync();
        var member = room.Members.SingleOrDefault(m => m.UserId == userId);
        if (member is null) return true; // повтор выхода после commit безопасен
        // Проверяем под блокировкой комнаты: запоздалый выход не затрагивает новый раунд.
        // 0 обозначает комнату, в которой ещё не было матчей; null — прежний API-клиент.
        if (expectedGameId.HasValue && expectedGameId.Value != (room.GameId ?? 0))
            throw new GameRuleException("room_round_changed", "Состояние комнаты изменилось.");
        if (room.Status == RoomStatus.InGame && room.GameId is int gameId)
        {
            var game = await mutation.LockGameAsync(gameId, userId, GameMode.Multiplayer);
            var player = game.Players.Single(p => p.UserId == userId);
            if (game.Status == GameStatus.InProgress)
            {
                if (player.Result == GamePlayerResult.Playing) player.Result = GamePlayerResult.GaveUp;
                var active = await db.ActiveGameParticipations.SingleOrDefaultAsync(p => p.UserId == userId && p.GameId == gameId);
                if (active is not null) db.ActiveGameParticipations.Remove(active);
                var remainingPlayers = game.Players.Where(p => p.Result == GamePlayerResult.Playing).ToList();
                if (remainingPlayers.Count <= 1) await mutation.FinishAsync(game, remainingPlayers.SingleOrDefault()?.UserId);
            }
        }
        db.RoomMembers.Remove(member);
        var remaining = room.Members.Where(m => m.UserId != userId).OrderBy(m => m.JoinedAt).ThenBy(m => m.Id).ToList();
        if (remaining.Count == 0) room.Status = RoomStatus.Closed;
        else
        {
            if (room.HostUserId == userId) room.HostUserId = remaining[0].UserId;
            if (room.Status != RoomStatus.InGame) room.Status = RoomStatus.Waiting;
        }
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return true;
    }
}
