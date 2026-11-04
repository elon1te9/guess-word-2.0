using GuessWord.Api.Realtime;
using GuessWord.Api.Features.Games;
using GuessWord.Contracts.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace GuessWord.Api.Features.Rooms
{
    [ApiController]
    [Route("api/room")]
    [Authorize]
    public class RoomController : ControllerBase
    {
        private readonly IRoomService _roomService;
        private readonly IHubContext<GameHub> _hubContext;

        public RoomController(
            IRoomService roomService,
            IHubContext<GameHub> hubContext)
        {
            _roomService = roomService;
            _hubContext = hubContext;
        }

        [HttpPost("create")]
        public async Task<IActionResult> CreateRoom()
        {
            var userId = GetUserId();
            var room = await _roomService.CreateRoomAsync(userId);
            return room is null ? BadRequest("У вас уже есть активная игра.") : Ok(room);
        }

        [HttpPost("join")]
        public async Task<IActionResult> JoinRoom([FromBody] JoinRoomRequestDto request)
        {
            if (string.IsNullOrWhiteSpace(request.Code))
                throw new GameRuleException("invalid_request", "Введите код комнаты.", 400);

            var userId = GetUserId();
            var room = await _roomService.JoinRoomAsync(userId, request.Code);

            if (room is null)
                return BadRequest("Не удалось присоединиться к комнате.");

            await _hubContext.Clients.Group($"room-{NormalizeRoomCode(room.Code)}")
                .SendAsync("RoomUpdated", room.Code);

            return Ok(room);
        }

        /// <summary>Возвращает текущую комнату пользователя, включая ожидание между раундами.</summary>
        [HttpGet("current")]
        public async Task<IActionResult> GetCurrentRoom()
        {
            var room = await _roomService.GetCurrentRoomAsync(GetUserId());
            return room is null ? NotFound() : Ok(room);
        }

        [HttpGet("{code}")]
        public async Task<IActionResult> GetRoom(string code)
        {
            var userId = GetUserId();
            var room = await _roomService.GetRoomAsync(userId, code);
            return room is null ? NotFound() : Ok(room);
        }

        [HttpPost("{code}/leave")]
        public async Task<IActionResult> LeaveRoom(string code)
        {
            var userId = GetUserId();
            var normalizedCode = NormalizeRoomCode(code);

            var before = await _roomService.GetRoomAsync(userId, normalizedCode);
            var result = await _roomService.LeaveRoomAsync(userId, normalizedCode);
            if (!result)
                return NotFound();

            await _hubContext.Clients.Group($"room-{normalizedCode}")
                .SendAsync("RoomUpdated", normalizedCode);

            if (before?.GameId is int gameId)
                await _hubContext.Clients.Group($"game-{gameId}").SendAsync("GameUpdated", gameId);
            return Ok();
        }

        private int GetUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrWhiteSpace(claim) || !int.TryParse(claim, out var userId))
                throw new GameRuleException("unauthorized", "Пользователь не авторизован.", 401);

            return userId;
        }

        private static string NormalizeRoomCode(string code)
        {
            return code.Trim().ToUpperInvariant();
        }
    }
}
