namespace GuessWord.Contracts.Responses
{
    /// <summary>Персональный ответ API для истории.</summary>
    public sealed record GameHistoryItemResponseDto
    {
        public DateTime Date { get; init; }
        public string GameType { get; init; } = string.Empty;
        public string Result { get; init; } = string.Empty;
        public int AttemptsCount { get; init; }
        public string SecretWord { get; init; } = string.Empty;
        public List<string> OpponentNames { get; init; } = new();
    }
}
