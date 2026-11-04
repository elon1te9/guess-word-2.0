using GuessWord.Api.Data;
using GuessWord.Contracts.Enums;
using GuessWord.Contracts.Responses;
using Microsoft.EntityFrameworkCore;

namespace GuessWord.Api.Features.Users;

public class UserProfileService
{
    private readonly AppDbContext _context;

    public UserProfileService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<UserProfileResponseDto?> GetProfileAsync(int userId)
    {
        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == userId);

        if (user is null)
            return null;

        var completedResults = await _context.GamePlayers
            .AsNoTracking()
            .Where(gp => gp.UserId == userId && gp.Game.Status == GameStatus.Finished)
            .Select(gp => gp.Result)
            .ToListAsync();

        var gamesCount = completedResults.Count;
        var winsCount = completedResults.Count(result => result == GamePlayerResult.Won);
        var lossesCount = completedResults.Count(result => result != GamePlayerResult.Won);
        var winRate = gamesCount == 0 ? 0 : winsCount * 100 / gamesCount;

        return new UserProfileResponseDto
        {
            Login = user.Login,
            Name = user.Name ?? user.Login,
            GamesCount = gamesCount,
            WinsCount = winsCount,
            LossesCount = lossesCount,
            WinRate = winRate
        };
    }
}
