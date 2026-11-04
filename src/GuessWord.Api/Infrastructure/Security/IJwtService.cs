using GuessWord.Api.Models;

namespace GuessWord.Api.Infrastructure.Security
{
    public interface IJwtService
    {
        string GenerateToken(User user);
    }
}
