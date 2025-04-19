namespace CurrencyConverter.Infrastructure.Authentication;

public class JwtSettings
{
    public required string Secret { get; set; }
    public required string Issuer { get; set; }
    public required string Audience { get; set; }
    public int ExpirationInMinutes { get; set; }
    public int DurationInMinutes { get => ExpirationInMinutes; set => ExpirationInMinutes = value; } // Added for compatibility with tests
}