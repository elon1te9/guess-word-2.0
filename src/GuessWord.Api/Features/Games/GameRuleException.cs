namespace GuessWord.Api.Features.Games;

public sealed class GameRuleException(string code, string message, int status = 409) : Exception(message)
{
    public string Code { get; } = code;
    public int Status { get; } = status;
}
