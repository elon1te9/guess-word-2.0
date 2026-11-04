using GuessWord.Api.Features.Auth;
using GuessWord.Api.Features.Leaderboard;
using GuessWord.Contracts.Requests;
using GuessWord.Contracts.Responses;

namespace GuessWord.Api.Features.Users;

public class UserService : IUserService
{
    private readonly AuthService _authService;
    private readonly UserProfileService _userProfileService;
    private readonly LeaderboardService _leaderboardService;

    public UserService(AuthService authService, UserProfileService userProfileService, LeaderboardService leaderboardService)
    {
        _authService = authService;
        _userProfileService = userProfileService;
        _leaderboardService = leaderboardService;
    }

    public Task<AuthResponseDto?> Register(RegisterRequestDto request) => _authService.Register(request);

    public Task<AuthResponseDto?> Login(LoginRequestDto request) => _authService.Login(request);

    public Task<UserProfileResponseDto?> GetProfileAsync(int userId) => _userProfileService.GetProfileAsync(userId);

    public Task<LeaderboardResponseDto> GetLeaderboardAsync() => _leaderboardService.GetLeaderboardAsync();
}
