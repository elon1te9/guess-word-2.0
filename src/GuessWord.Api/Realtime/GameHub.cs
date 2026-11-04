using System.Security.Claims;
using GuessWord.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace GuessWord.Api.Realtime;

[Authorize]
public class GameHub(AppDbContext db) : Hub
{
    private int UserId => int.TryParse(Context.User?.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
        ? id : throw new HubException("Доступ запрещён.");

    public async Task JoinRoomGroup(string roomCode)
    {
        var code = roomCode.Trim().ToUpperInvariant();
        if (!await db.RoomMembers.AnyAsync(m => m.UserId == UserId && m.Room.Code == code))
            throw new HubException("Доступ запрещён.");
        await Groups.AddToGroupAsync(Context.ConnectionId, $"room-{code}");
    }
    public Task LeaveRoomGroup(string roomCode) => Groups.RemoveFromGroupAsync(Context.ConnectionId, $"room-{roomCode.Trim().ToUpperInvariant()}");
    public async Task JoinGameGroup(int gameId)
    {
        if (!await db.GamePlayers.AnyAsync(p => p.GameId == gameId && p.UserId == UserId && p.Game.Mode == GuessWord.Contracts.Enums.GameMode.Multiplayer
            && (p.Game.Status == GuessWord.Contracts.Enums.GameStatus.Finished || db.RoomMembers.Any(m => m.RoomId == p.Game.RoomId && m.UserId == UserId))))
            throw new HubException("Доступ запрещён.");
        await Groups.AddToGroupAsync(Context.ConnectionId, $"game-{gameId}");
    }
    public Task LeaveGameGroup(int gameId) => Groups.RemoveFromGroupAsync(Context.ConnectionId, $"game-{gameId}");
}
