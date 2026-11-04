namespace GuessWord.Contracts.Requests
{
    public class SubmitGuessRequestDto
    {
        public int GameId { get; set; }
        public string Word { get; set; } = null!;
    }
}
