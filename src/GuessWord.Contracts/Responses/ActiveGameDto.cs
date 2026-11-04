using GuessWord.Contracts.Enums;

namespace GuessWord.Contracts.Responses
{
    public class ActiveGameDto
    {
        public int GameId { get; set; }
        public GameMode GameMode { get; set; }
        public GameStatus Status { get; set; }
        public string? RoomCode { get; set; }
    }
}
