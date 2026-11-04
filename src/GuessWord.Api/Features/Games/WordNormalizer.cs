namespace GuessWord.Api.Features.Games;

public static class WordNormalizer
{
    public static string Normalize(string? word) =>
        (word ?? string.Empty).Trim().ToLowerInvariant().Replace('ё', 'е');
}
