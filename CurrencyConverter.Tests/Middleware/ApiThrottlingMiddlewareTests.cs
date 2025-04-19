using CurrencyConverter.API.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using System.Text.Json;

namespace CurrencyConverter.Tests.Middleware;

public class ApiThrottlingMiddlewareTests
{
    private readonly Mock<ILogger<ApiThrottlingMiddleware>> _mockLogger;
    private readonly ApiThrottlingMiddleware _middleware;
    private readonly Mock<RequestDelegate> _nextMock;

    public ApiThrottlingMiddlewareTests()
    {
        _mockLogger = new Mock<ILogger<ApiThrottlingMiddleware>>();
        _nextMock = new Mock<RequestDelegate>();
        _middleware = new ApiThrottlingMiddleware(_nextMock.Object, _mockLogger.Object, 2, 60);
    }

    [Fact]
    public async Task InvokeAsync_UnderRateLimit_CallsNext()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        httpContext.Connection.RemoteIpAddress = new System.Net.IPAddress(new byte[] { 127, 0, 0, 1 });

        // Act
        await _middleware.InvokeAsync(httpContext);

        // Assert
        _nextMock.Verify(next => next(httpContext), Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_AuthenticatedUser_UsesClientId()
    {
        // Arrange
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "test-user")
        }, "mock"));

        var httpContext = new DefaultHttpContext();
        httpContext.User = user;

        // Act
        await _middleware.InvokeAsync(httpContext);

        // Assert
        _nextMock.Verify(next => next(httpContext), Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_ExceedsRateLimit_Returns429()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        httpContext.Connection.RemoteIpAddress = new System.Net.IPAddress(new byte[] { 127, 0, 0, 1 });
        httpContext.Response.Body = new MemoryStream();

        // Act - Call multiple times to exceed rate limit
        await _middleware.InvokeAsync(httpContext); // First call
        await _middleware.InvokeAsync(httpContext); // Second call
        await _middleware.InvokeAsync(httpContext); // Third call - should exceed limit

        // Assert
        Assert.Equal(StatusCodes.Status429TooManyRequests, httpContext.Response.StatusCode);
        Assert.Equal("application/json", httpContext.Response.ContentType);

        // Verify response body contains error message
        httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(httpContext.Response.Body);
        var responseBody = await reader.ReadToEndAsync();
        Assert.Contains("Rate limit exceeded", responseBody);
        
        // Verify next middleware was only called twice (limit is 2)
        _nextMock.Verify(next => next(It.IsAny<HttpContext>()), Times.Exactly(2));
    }
}