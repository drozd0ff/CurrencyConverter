using System.Security.Claims;
using CurrencyConverter.API.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace CurrencyConverter.Tests.Middleware;

public class RequestResponseLoggingMiddlewareTests
{
    private readonly Mock<ILogger<RequestResponseLoggingMiddleware>> _mockLogger;
    private readonly RequestResponseLoggingMiddleware _middleware;
    private readonly Mock<RequestDelegate> _nextMock;

    public RequestResponseLoggingMiddlewareTests()
    {
        _mockLogger = new Mock<ILogger<RequestResponseLoggingMiddleware>>();
        _nextMock = new Mock<RequestDelegate>();
        _middleware = new RequestResponseLoggingMiddleware(_nextMock.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task InvokeAsync_LogsRequestDetails_WhenRequestProcessedSuccessfully()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        httpContext.Connection.RemoteIpAddress = new System.Net.IPAddress(new byte[] { 127, 0, 0, 1 });
        httpContext.Request.Method = "GET";
        httpContext.Request.Path = "/api/v1/rates/latest";
        httpContext.Response.StatusCode = 200;
        
        _nextMock.Setup(next => next(httpContext))
            .Returns(Task.CompletedTask);

        // Act
        await _middleware.InvokeAsync(httpContext);

        // Assert
        _nextMock.Verify(next => next(httpContext), Times.Once);
        
        // Verify that the correlation ID is set in the response headers
        Assert.True(httpContext.Response.Headers.ContainsKey("X-Correlation-ID"));
        Assert.True(httpContext.Response.Headers["X-Correlation-ID"].Count > 0);
        
        // We can't directly verify the logging contents with Moq in a simple way,
        // but we can verify that logging was called at the Information level
        _mockLogger.Verify(
            x => x.Log(
                It.Is<LogLevel>(l => l == LogLevel.Information),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
    
    [Fact]
    public async Task InvokeAsync_LogsRequestDetails_ForAuthenticatedUser()
    {
        // Arrange
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "test-user")
        }, "mock"));

        var httpContext = new DefaultHttpContext();
        httpContext.User = user;
        httpContext.Request.Method = "GET";
        httpContext.Request.Path = "/api/v1/rates/latest";
        httpContext.Response.StatusCode = 200;
        
        _nextMock.Setup(next => next(httpContext))
            .Returns(Task.CompletedTask);

        // Act
        await _middleware.InvokeAsync(httpContext);

        // Assert
        _nextMock.Verify(next => next(httpContext), Times.Once);
        
        // Verify that logging was called at the Information level
        _mockLogger.Verify(
            x => x.Log(
                It.Is<LogLevel>(l => l == LogLevel.Information),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
    
    [Fact]
    public async Task InvokeAsync_LogsError_WhenExceptionOccurs()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = "GET";
        httpContext.Request.Path = "/api/v1/rates/latest";
        
        var testException = new InvalidOperationException("Test exception");
        
        _nextMock.Setup(next => next(httpContext))
            .ThrowsAsync(testException);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () => 
            await _middleware.InvokeAsync(httpContext));
            
        // Verify that logging was called at the Error level with the exception
        _mockLogger.Verify(
            x => x.Log(
                It.Is<LogLevel>(l => l == LogLevel.Error),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.Is<Exception>(e => e == testException),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}