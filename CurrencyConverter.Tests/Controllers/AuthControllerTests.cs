using CurrencyConverter.API.Controllers;
using CurrencyConverter.Core.Models;
using CurrencyConverter.Infrastructure.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace CurrencyConverter.Tests.Controllers;

public class AuthControllerTests
{
    private readonly Mock<IJwtTokenService> _mockJwtTokenService;
    private readonly Mock<ILogger<AuthController>> _mockLogger;
    private readonly AuthController _controller;

    public AuthControllerTests()
    {
        _mockJwtTokenService = new Mock<IJwtTokenService>();
        _mockLogger = new Mock<ILogger<AuthController>>();
        _controller = new AuthController(_mockJwtTokenService.Object, _mockLogger.Object);
    }

    [Fact]
    public void Constructor_WithNullJwtTokenService_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new AuthController(null!, _mockLogger.Object));
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new AuthController(_mockJwtTokenService.Object, null!));
    }

    [Fact]
    public void Login_WithAdminCredentials_ReturnsOkWithToken()
    {
        // Arrange
        var request = new AuthController.LoginRequest 
        { 
            Username = "admin", 
            Password = "admin"
        };
        var expectedToken = "test-admin-token";
        _mockJwtTokenService.Setup(s => s.GenerateToken(request.Username, It.IsAny<string[]>()))
            .Returns(expectedToken);

        // Act
        var result = _controller.Login(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<AuthController.LoginResponse>>(okResult.Value);
        Assert.True(response.Success);
        Assert.Equal(expectedToken, response.Data!.Token);
        Assert.Equal("Bearer", response.Data.TokenType);
        Assert.Equal(60 * 60, response.Data.ExpiresIn);
        Assert.Contains("Admin", response.Data.Roles);
    }

    [Fact]
    public void Login_WithPremiumCredentials_ReturnsOkWithToken()
    {
        // Arrange
        var request = new AuthController.LoginRequest 
        { 
            Username = "premium", 
            Password = "premium"
        };
        var expectedToken = "test-premium-token";
        _mockJwtTokenService.Setup(s => s.GenerateToken(request.Username, It.IsAny<string[]>()))
            .Returns(expectedToken);

        // Act
        var result = _controller.Login(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<AuthController.LoginResponse>>(okResult.Value);
        Assert.True(response.Success);
        Assert.Equal(expectedToken, response.Data!.Token);
        Assert.Equal("Bearer", response.Data.TokenType);
        Assert.Equal(60 * 60, response.Data.ExpiresIn);
        Assert.Contains("Premium", response.Data.Roles);
    }

    [Fact]
    public void Login_WithUserCredentials_ReturnsOkWithToken()
    {
        // Arrange
        var request = new AuthController.LoginRequest 
        { 
            Username = "user", 
            Password = "password"
        };
        var expectedToken = "test-user-token";
        _mockJwtTokenService.Setup(s => s.GenerateToken(request.Username, It.IsAny<string[]>()))
            .Returns(expectedToken);

        // Act
        var result = _controller.Login(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<AuthController.LoginResponse>>(okResult.Value);
        Assert.True(response.Success);
        Assert.Equal(expectedToken, response.Data!.Token);
        Assert.Equal("Bearer", response.Data.TokenType);
        Assert.Equal(60 * 60, response.Data.ExpiresIn);
        Assert.Contains("User", response.Data.Roles);
    }

    [Fact]
    public void Login_WithInvalidCredentials_ReturnsUnauthorized()
    {
        // Arrange
        var request = new AuthController.LoginRequest 
        { 
            Username = "invalid", 
            Password = "invalid"
        };

        // Act
        var result = _controller.Login(request);

        // Assert
        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<AuthController.LoginResponse>>(unauthorizedResult.Value);
        Assert.False(response.Success);
        Assert.Equal("Invalid username or password", response.Message);
        Assert.Null(response.Data);
    }
}