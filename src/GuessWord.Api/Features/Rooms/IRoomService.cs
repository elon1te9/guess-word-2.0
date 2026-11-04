using GuessWord.Contracts.Responses;

namespace GuessWord.Api.Features.Rooms
{
    public interface IRoomService
    {
        Task<RoomResponseDto?> CreateRoomAsync(int userId);
        Task<RoomResponseDto?> JoinRoomAsync(int userId, string code);
        Task<RoomResponseDto?> GetCurrentRoomAsync(int userId);
        Task<RoomResponseDto?> GetRoomAsync(int userId, string code);
        Task<bool> LeaveRoomAsync(int userId, string code);
    }
}
