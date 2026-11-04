namespace GuessWord.Client.Infrastructure.Http;

public sealed class ApiRequestException(string code, string message, int status) : Exception(message)
{
    public string Code { get; } = code;
    public int Status { get; } = status;
}
