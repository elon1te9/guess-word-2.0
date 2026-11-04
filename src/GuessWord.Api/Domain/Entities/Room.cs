using GuessWord.Contracts.Enums;

namespace GuessWord.Api.Models
{
    public class Room
    {
        public int Id { get; set; }
        public string Code { get; set; } = null!;

        public int HostUserId { get; set; }
        public User HostUser { get; set; } = null!;

        public List<RoomMember> Members { get; set; } = new();

        public RoomStatus Status { get; set; } = RoomStatus.Waiting;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public int? GameId { get; set; }

        public Game? Game { get; set; }
    }
}
