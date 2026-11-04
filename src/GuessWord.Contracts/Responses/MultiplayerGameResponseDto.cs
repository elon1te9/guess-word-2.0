using GuessWord.Contracts.Enums;

namespace GuessWord.Contracts.Responses
{
    /// <summary>Персональный ответ API для матча.</summary>
    public sealed record MultiplayerGameResponseDto
    {
        public int GameId { get; init; }
        public GameStatus Status { get; init; }
        public GamePlayerResult PlayerResult { get; init; }
        public string? SecretWord { get; init; }
        public int AttemptsCount { get; init; }
        public List<GameAttemptResponseDto> Attempts { get; init; } = new();
        public GameAttemptResponseDto? LastAttempt { get; init; }
        public bool LastAttemptWasRepeated { get; init; }
        public List<GamePlayerStateDto> Players { get; init; } = new();
        public bool IsWinner { get; init; }
    }
}
