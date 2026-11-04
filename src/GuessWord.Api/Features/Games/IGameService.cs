using GuessWord.Contracts.Requests;
using GuessWord.Contracts.Responses;

namespace GuessWord.Api.Features.Games
{
    public interface IGameService
    {
        Task<ActiveGameDto?> GetActiveGameAsync(int userId);
        Task<SingleGameResponseDto> StartSingleGameAsync(int userId);
        Task<int?> StartMultiplayerGameAsync(int userId, string roomCode);
        Task<GameStateDto?> GetGameStateAsync(int userId, int gameId);
        Task<MultiplayerGameResponseDto?> GetMultiplayerGameAsync(int userId, int gameId);
        Task<SingleGameResponseDto?> GetCurrentSingleGameAsync(int userId);
        Task<MultiplayerGameResponseDto?> SubmitMultiplayerGuessAsync(int userId, SubmitGuessRequestDto request);
        Task<GameStateDto?> GiveUpMultiplayerGameAsync(int userId, int gameId);
        Task<SingleGameResponseDto> SubmitGuessAsync(int userId, SubmitGuessRequestDto request);
        Task<SingleGameResponseDto> GiveUpSingleGameAsync(int userId, int gameId);
        Task<List<GameHistoryItemResponseDto>> GetHistoryAsync(int userId);
    }
}
