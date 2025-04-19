using CurrencyConverter.API.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;

namespace CurrencyConverter.Tests.Middleware;

public class ExceptionHandlingMiddlewareTests
{
    private readonly Mock<ILogger<ExceptionHandlingMiddleware>> _mockLogger;
    private readonly ExceptionHandlingMiddleware _middleware;
    private readonly Mock<RequestDelegate> _nextMock;

    public ExceptionHandlingMiddlewareTests()
    {
        _mockLogger = new Mock<ILogger<ExceptionHandlingMiddleware>>();
        _nextMock = new Mock<RequestDelegate>();
        _middleware = new ExceptionHandlingMiddleware(_nextMock.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task InvokeAsync_NoException_CallsNextDelegate()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        _nextMock.Setup(next => next(httpContext)).Returns(Task.CompletedTask);

        // Act
        await _middleware.InvokeAsync(httpContext);

        // Assert
        _nextMock.Verify(next => next(httpContext), Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_ArgumentException_Returns400()
    {
        // Arrange
        var exception = new ArgumentException("Invalid argument");
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();

        _nextMock.Setup(next => next(httpContext)).ThrowsAsync(exception);

        // Act
        await _middleware.InvokeAsync(httpContext);

        // Assert
        Assert.Equal((int)HttpStatusCode.BadRequest, httpContext.Response.StatusCode);
        Assert.Equal("application/json", httpContext.Response.ContentType);

        // Check response body contains error message
        httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(httpContext.Response.Body);
        var responseBody = await reader.ReadToEndAsync();
        Assert.Contains(exception.Message, responseBody);
    }

    [Fact]
    public async Task InvokeAsync_InvalidOperationException_Returns400()
    {
        // Arrange
        var exception = new InvalidOperationException("Invalid operation");
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();

        _nextMock.Setup(next => next(httpContext)).ThrowsAsync(exception);

        // Act
        await _middleware.InvokeAsync(httpContext);

        // Assert
        Assert.Equal((int)HttpStatusCode.BadRequest, httpContext.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_UnauthorizedAccessException_Returns401()
    {
        // Arrange
        var exception = new UnauthorizedAccessException("Unauthorized");
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();

        _nextMock.Setup(next => next(httpContext)).ThrowsAsync(exception);

        // Act
        await _middleware.InvokeAsync(httpContext);

        // Assert
        Assert.Equal((int)HttpStatusCode.Unauthorized, httpContext.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_HttpRequestException_Returns503()
    {
        // Arrange
        var exception = new HttpRequestException("Service unavailable");
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();

        _nextMock.Setup(next => next(httpContext)).ThrowsAsync(exception);

        // Act
        await _middleware.InvokeAsync(httpContext);

        // Assert
        Assert.Equal((int)HttpStatusCode.ServiceUnavailable, httpContext.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_UnhandledException_Returns500()
    {
        // Arrange
        var exception = new Exception("Unexpected error");
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();

        _nextMock.Setup(next => next(httpContext)).ThrowsAsync(exception);

        // Act
        await _middleware.InvokeAsync(httpContext);

        // Assert
        Assert.Equal((int)HttpStatusCode.InternalServerError, httpContext.Response.StatusCode);
        
        // Verify error was logged
        _mockLogger.Verify(
            x => x.Log(
                It.Is<LogLevel>(l => l == LogLevel.Error),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.Is<Exception>(e => e == exception),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}