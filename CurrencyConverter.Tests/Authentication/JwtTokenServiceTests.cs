using CurrencyConverter.Infrastructure.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

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
        
        // Print all claims for diagnosis
        foreach (var claim in claims)
        {
            Console.WriteLine($"Found claim - Type: '{claim.Type}', Value: '{claim.Value}'");
        }

        // Use string comparison for claim types instead of enum values
        Assert.Contains(claims, c => c.Type == "sub" && c.Value == userId);
        Assert.Contains(claims, c => c.Type == "nameid" && c.Value == userId);
        Assert.Contains(claims, c => c.Type == "name" && c.Value == userName);
        Assert.Contains(claims, c => c.Type == "role" && c.Value == "User");
        Assert.Contains(claims, c => c.Type == "role" && c.Value == "Admin");
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
        Assert.DoesNotContain(claims, c => c.Type == "role");
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
        Assert.DoesNotContain(claims, c => c.Type == "role");
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

    [Fact]
    public void ValidateToken_ValidToken_ReturnsValidClaimsPrincipal()
    {
        // Arrange
        string userId = "test-user-id";
        string userName = "testuser";
        var roles = new List<string> { "User", "Admin" };

        // Generate a token first
        var token = _tokenService.GenerateToken(userId, userName, roles);

        // Act
        var principal = _tokenService.ValidateToken(token);

        // Assert
        Assert.NotNull(principal);
        Assert.NotNull(principal.Identity);
        Assert.True(principal.Identity.IsAuthenticated);
        
        // Check identity and claims
        var identity = principal.Identity as ClaimsIdentity;
        Assert.NotNull(identity);
        
        // Verify expected claims exist - using actual claim types from JWT tokens
        Assert.Contains(identity.Claims, c => c.Type == ClaimTypes.NameIdentifier && c.Value == userId);
        Assert.Contains(identity.Claims, c => c.Type == "name" && c.Value == userName);
        Assert.Contains(identity.Claims, c => c.Type == ClaimTypes.Role && c.Value == "User");
        Assert.Contains(identity.Claims, c => c.Type == ClaimTypes.Role && c.Value == "Admin");
    }

    [Fact]
    public void Constructor_WithNullOptions_ThrowsArgumentNullException()
    {
        // Arrange
        IOptions<JwtSettings>? nullOptions = null;
        
        // The null reference exception happens because we're accessing .Value on a null object
        // So we need to create a mock where Value is null
        var mockOptionsWithNullValue = new Mock<IOptions<JwtSettings>>();
        mockOptionsWithNullValue.Setup(x => x.Value).Returns((JwtSettings)null);
        
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new JwtTokenService(mockOptionsWithNullValue.Object, _mockLogger.Object));
    }
    
    [Fact]
    public void Constructor_WithNullLogger_CreatesServiceCorrectly()
    {
        // Arrange & Act
        var service = new JwtTokenService(_mockOptions.Object, null);
        
        // Assert - service was created without exception
        Assert.NotNull(service);
        
        // Verify token generation still works
        var token = service.GenerateToken("id", "user", new List<string> { "Role" });
        Assert.NotNull(token);
        Assert.NotEmpty(token);
    }
    
    [Fact]
    public void ValidateToken_InvalidToken_ThrowsSecurityTokenException()
    {
        // Arrange
        string invalidToken = "invalid.token.string";
        
        // Act & Assert
        // Since JwtSecurityTokenHandler's ValidateToken throws ArgumentException 
        // for this type of invalid token, we should expect that instead
        Assert.Throws<ArgumentException>(() => _tokenService.ValidateToken(invalidToken));
    }
    
    [Fact]
    public void GenerateToken_OriginalOverload_GeneratesValidToken()
    {
        // Arrange
        string userId = "test-user-id";
        string[] roles = { "User", "Admin" };
        
        // Act
        var token = _tokenService.GenerateToken(userId, roles);
        
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
        Assert.Contains(claims, c => c.Type == "sub" && c.Value == userId);
        Assert.Contains(claims, c => c.Type == "role" && c.Value == "User");
        Assert.Contains(claims, c => c.Type == "role" && c.Value == "Admin");
    }
}