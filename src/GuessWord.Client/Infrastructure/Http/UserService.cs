using GuessWord.Client.Infrastructure.Http;
using GuessWord.Contracts.Responses;

namespace GuessWord.Client.Infrastructure.Http;

public class UserService
{
    private readonly ApiRequestService _apiRequestService;

    public UserService(ApiRequestService apiRequestService)
    {
        _apiRequestService = apiRequestService;
    }

    public async Task<UserProfileResponseDto?> GetProfileAsync()
    {
        return await _apiRequestService.GetAsync<UserProfileResponseDto>("api/user/profile");
    }

    public async Task<LeaderboardResponseDto?> GetLeaderboardAsync()
    {
        return await _apiRequestService.GetAsync<LeaderboardResponseDto>("api/user/leaderboard");
    }
}
