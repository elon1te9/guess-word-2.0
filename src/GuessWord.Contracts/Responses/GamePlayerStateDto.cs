namespace GuessWord.Contracts.Responses
{
    public class GamePlayerStateDto
    {
        public int UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public int AttemptsCount { get; set; }
        public int? BestRank { get; set; }
        public GuessWord.Contracts.Enums.GamePlayerResult Result { get; set; }
        public List<GameAttemptDto> Attempts { get; set; } = new();
    }
}
