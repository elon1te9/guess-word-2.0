using GuessWord.Contracts.Enums;

namespace GuessWord.Contracts.Responses
{
    public class GameStateDto
    {
        public string? RoomCode { get; set; }
        public int GameId { get; set; }
        public GameStatus Status { get; set; }
        public string? SecretWord { get; set; }
        public int? SecretWordLength { get; set; }
        public List<GamePlayerStateDto> Players { get; set; } = new();
        public int? WinnerId { get; set; }
    }
}
