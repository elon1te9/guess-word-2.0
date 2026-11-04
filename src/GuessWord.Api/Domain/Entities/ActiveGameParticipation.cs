namespace GuessWord.Api.Models;

// Только действующее участие. История остаётся в GamePlayer.
public class ActiveGameParticipation
{
    public int UserId { get; set; }
    public int GameId { get; set; }
    public GamePlayer Player { get; set; } = null!;
}
