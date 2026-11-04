using GuessWord.Api.Features.History;
using GuessWord.Contracts.Requests;
using GuessWord.Contracts.Responses;

namespace GuessWord.Api.Features.Games;

public class GameService : IGameService
{
    private readonly GameHistoryService _gameHistoryService;
    private readonly GameQueries _gameQueries;
    private readonly GameResponseBuilder _gameResponseBuilder;
    private readonly MultiplayerGameCommands _multiplayerGameCommands;
    private readonly SingleGameCommands _singleGameCommands;

    public GameService(GameHistoryService gameHistoryService, GameQueries gameQueries, GameResponseBuilder gameResponseBuilder, MultiplayerGameCommands multiplayerGameCommands, SingleGameCommands singleGameCommands)
    {
        _gameHistoryService = gameHistoryService;
        _gameQueries = gameQueries;
        _gameResponseBuilder = gameResponseBuilder;
        _multiplayerGameCommands = multiplayerGameCommands;
        _singleGameCommands = singleGameCommands;
    }

    public Task<ActiveGameDto?> GetActiveGameAsync(int userId) => _gameQueries.GetActiveGameAsync(userId);

    public Task<SingleGameResponseDto> StartSingleGameAsync(int userId) => _singleGameCommands.StartSingleGameAsync(userId);

    public Task<int?> StartMultiplayerGameAsync(int userId, string roomCode) => _multiplayerGameCommands.StartMultiplayerGameAsync(userId, roomCode);

    public Task<GameStateDto?> GetGameStateAsync(int userId, int gameId) => _gameResponseBuilder.GetGameStateAsync(userId, gameId);

    public Task<MultiplayerGameResponseDto?> GetMultiplayerGameAsync(int userId, int gameId) => _gameQueries.GetMultiplayerGameAsync(userId, gameId);

    public Task<SingleGameResponseDto?> GetCurrentSingleGameAsync(int userId) => _gameQueries.GetCurrentSingleGameAsync(userId);

    public Task<MultiplayerGameResponseDto?> SubmitMultiplayerGuessAsync(int userId, SubmitGuessRequestDto request) => _multiplayerGameCommands.SubmitMultiplayerGuessAsync(userId, request);

    public Task<GameStateDto?> GiveUpMultiplayerGameAsync(int userId, int gameId) => _multiplayerGameCommands.GiveUpMultiplayerGameAsync(userId, gameId);

    public Task<SingleGameResponseDto> SubmitGuessAsync(int userId, SubmitGuessRequestDto request) => _singleGameCommands.SubmitGuessAsync(userId, request);

    public Task<SingleGameResponseDto> GiveUpSingleGameAsync(int userId, int gameId) => _singleGameCommands.GiveUpSingleGameAsync(userId, gameId);

    public Task<List<GameHistoryItemResponseDto>> GetHistoryAsync(int userId) => _gameHistoryService.GetHistoryAsync(userId);
}
