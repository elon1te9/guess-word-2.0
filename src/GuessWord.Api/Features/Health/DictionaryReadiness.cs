using GuessWord.Api.Data;
using GuessWord.Api.Features.Games;
using Microsoft.EntityFrameworkCore;

namespace GuessWord.Api.Features.Health;

public interface IDictionaryReadiness
{
    Task<bool> IsReadyAsync(CancellationToken cancellationToken);
}

public sealed class DictionaryReadiness(AppDbContext db, IWebHostEnvironment environment,
    IConfiguration configuration) : IDictionaryReadiness
{
    public async Task<bool> IsReadyAsync(CancellationToken cancellationToken)
    {
        try
        {
            var words = ReadWords("AllWordsPath", "all-words.txt");
            var secrets = ReadWords("SecretWordsPath", "secret-words.txt");
            if (words.Count < 20_000 || secrets.Count == 0 || !secrets.IsSubsetOf(words)) return false;
            var actual = await db.Words.AsNoTracking()
                .Select(w => new { w.Text, w.CanBeSecret }).ToListAsync(cancellationToken);
            if (actual.Count != words.Count || !words.SetEquals(actual.Select(w => w.Text))
                || !secrets.SetEquals(actual.Where(w => w.CanBeSecret).Select(w => w.Text))) return false;
            var invalid = await db.Database.SqlQuery<int>(
                $"""SELECT count(*)::int AS "Value" FROM "Words" WHERE "Embedding" IS NULL OR vector_dims("Embedding") <> 800 OR vector_norm("Embedding") <= 0""")
                .SingleAsync(cancellationToken);
            return invalid == 0;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception)
        {
            // Готовность не раскрывает подключение, пути или исключения БД.
            return false;
        }
    }

    private HashSet<string> ReadWords(string key, string file)
    {
        var path = configuration[$"Dictionary:{key}"];
        path = string.IsNullOrWhiteSpace(path) ? Path.Combine("Resources", file) : path;
        if (!Path.IsPathRooted(path)) path = Path.Combine(environment.ContentRootPath, path);
        return File.ReadLines(path).Select(WordNormalizer.Normalize)
            .Where(w => w.Length > 0).ToHashSet(StringComparer.Ordinal);
    }
}
