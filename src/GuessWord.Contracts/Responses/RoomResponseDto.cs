using GuessWord.Contracts.Enums;

namespace GuessWord.Contracts.Responses
{
    /// <summary>Персональный ответ API для комнаты.</summary>
    public sealed record RoomResponseDto
    {
        public string Code { get; init; } = string.Empty;
        public List<RoomMemberDto> Players { get; init; } = new();
        public RoomStatus Status { get; init; }
        public int? GameId { get; init; }
        public bool IsFull { get; init; }
        public bool CanStartGame { get; init; }
        public bool IsHost { get; init; }
    }
}
