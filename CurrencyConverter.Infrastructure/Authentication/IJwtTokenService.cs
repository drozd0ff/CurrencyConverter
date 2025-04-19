using System.Security.Claims;

namespace CurrencyConverter.Infrastructure.Authentication;

public interface IJwtTokenService
{
    string GenerateToken(string userId, string[] roles);
    string GenerateToken(string userId, string userName, List<string>? roles);
    ClaimsPrincipal ValidateToken(string token);
}