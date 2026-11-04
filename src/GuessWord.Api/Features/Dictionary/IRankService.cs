namespace GuessWord.Api.Features.Dictionary
{
    public interface IRankService
    {
        Task PrepareRankingAsync(int secretWordId);
        Task<int> GetRankAsync(int secretWordId, int guessWordId);
    }
}
