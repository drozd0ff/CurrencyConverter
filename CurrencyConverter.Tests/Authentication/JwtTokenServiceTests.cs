using CurrencyConverter.Infrastructure.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

#nullable enable
namespace CurrencyConverter.Tests.Authentication;

public class JwtTokenServiceTests
{
    private readonly Mock<IOptions<JwtSettings>> _mockOptions;
    private readonly Mock<ILogger<JwtTokenService>> _mockLogger;
    private readonly JwtTokenService _tokenService;
    private readonly JwtSettings _jwtSettings;

    public JwtTokenServiceTests()
    {
        _jwtSettings = new JwtSettings
        {
            Secret = "this_is_a_very_secure_test_secret_key_for_jwt_token_generation_testing",
            Issuer = "TestIssuer",
            Audience = "TestAudience",
            DurationInMinutes = 60
        };

        _mockOptions = new Mock<IOptions<JwtSettings>>();
        _mockOptions.Setup(x => x.Value).Returns(_jwtSettings);
        
        _mockLogger = new Mock<ILogger<JwtTokenService>>();
        _tokenService = new JwtTokenService(_mockOptions.Object, _mockLogger.Object);
    }

    [Fact]
    public void GenerateToken_ValidUser_ReturnsValidToken()
    {
        // Arrange
        string userId = "test-user-id";
        string userName = "testuser";
        var roles = new List<string> { "User", "Admin" };

        // Act
        var token = _tokenService.GenerateToken(userId, userName, roles);

        // Assert
        Assert.NotNull(token);
        Assert.NotEmpty(token);

        // Validate token content
        var tokenHandler = new JwtSecurityTokenHandler();
        var jsonToken = tokenHandler.ReadToken(token) as JwtSecurityToken;
        
        Assert.NotNull(jsonToken);
        Assert.Equal(_jwtSettings.Issuer, jsonToken.Issuer);
        Assert.Equal(_jwtSettings.Audience, jsonToken.Payload["aud"].ToString());
        
        // Check claims
        var claims = jsonToken.Claims.ToList();
        Assert.Contains(claims, c => c.Type == JwtRegisteredClaimNames.Sub && c.Value == userId);
        Assert.Contains(claims, c => c.Type == JwtRegisteredClaimNames.NameId && c.Value == userId);
        Assert.Contains(claims, c => c.Type == JwtRegisteredClaimNames.Name && c.Value == userName);
        Assert.Contains(claims, c => c.Type == ClaimTypes.Role && c.Value == "User");
        Assert.Contains(claims, c => c.Type == ClaimTypes.Role && c.Value == "Admin");
    }

    [Fact]
    public void GenerateToken_NoRoles_ReturnsTokenWithoutRoleClaims()
    {
        // Arrange
        string userId = "test-user-id";
        string userName = "testuser";
        var roles = new List<string>(); // Empty roles list

        // Act
        var token = _tokenService.GenerateToken(userId, userName, roles);

        // Assert
        Assert.NotNull(token);
        Assert.NotEmpty(token);

        var tokenHandler = new JwtSecurityTokenHandler();
        var jsonToken = tokenHandler.ReadToken(token) as JwtSecurityToken;
        
        Assert.NotNull(jsonToken);
        
        // Check there are no role claims
        var claims = jsonToken.Claims.ToList();
        Assert.DoesNotContain(claims, c => c.Type == ClaimTypes.Role);
    }

    [Fact]
    public void GenerateToken_NullRoles_ReturnsTokenWithoutRoleClaims()
    {
        // Arrange
        string userId = "test-user-id";
        string userName = "testuser";
        List<string>? roles = null;

        // Act
        var token = _tokenService.GenerateToken(userId, userName, roles);

        // Assert
        Assert.NotNull(token);
        Assert.NotEmpty(token);

        var tokenHandler = new JwtSecurityTokenHandler();
        var jsonToken = tokenHandler.ReadToken(token) as JwtSecurityToken;
        
        Assert.NotNull(jsonToken);
        
        // Check there are no role claims
        var claims = jsonToken.Claims.ToList();
        Assert.DoesNotContain(claims, c => c.Type == ClaimTypes.Role);
    }

    [Fact]
    public void GenerateToken_WithExpiration_SetsCorrectExpiry()
    {
        // Arrange
        string userId = "test-user-id";
        string userName = "testuser";
        var roles = new List<string> { "User" };
        
        // Get current time for comparison
        var now = DateTime.UtcNow;

        // Act
        var token = _tokenService.GenerateToken(userId, userName, roles);

        // Assert
        var tokenHandler = new JwtSecurityTokenHandler();
        var jsonToken = tokenHandler.ReadToken(token) as JwtSecurityToken;
        
        Assert.NotNull(jsonToken);
        
        // Check expiration is set correctly (with small tolerance)
        var expectedExpiry = now.AddMinutes(_jwtSettings.DurationInMinutes);
        var timeDifference = jsonToken.ValidTo - expectedExpiry;
        
        // Allow for a small time difference (5 seconds) due to test execution timing
        Assert.True(Math.Abs(timeDifference.TotalSeconds) < 5);
    }
}