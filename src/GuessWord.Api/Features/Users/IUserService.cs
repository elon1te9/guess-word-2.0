using GuessWord.Contracts.Requests;
using GuessWord.Contracts.Responses;

namespace GuessWord.Api.Features.Users
{
    public interface IUserService
    {
        Task<AuthResponseDto?> Register(RegisterRequestDto request);
        Task<AuthResponseDto?> Login(LoginRequestDto request);
        Task<UserProfileResponseDto?> GetProfileAsync(int userId);
        Task<LeaderboardResponseDto> GetLeaderboardAsync();
    }
}
