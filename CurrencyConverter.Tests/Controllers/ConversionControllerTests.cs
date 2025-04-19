using CurrencyConverter.API.Controllers;
using CurrencyConverter.Core.Interfaces;
using CurrencyConverter.Core.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace CurrencyConverter.Tests.Controllers;

public class ConversionControllerTests
{
    private readonly Mock<IExchangeRateProviderFactory> _mockFactory;
    private readonly Mock<IExchangeRateProvider> _mockProvider;
    private readonly Mock<ILogger<ConversionController>> _mockLogger;
    private readonly ConversionController _controller;

    public ConversionControllerTests()
    {
        _mockFactory = new Mock<IExchangeRateProviderFactory>();
        _mockProvider = new Mock<IExchangeRateProvider>();
        _mockLogger = new Mock<ILogger<ConversionController>>();
        
        _mockFactory.Setup(f => f.CreateProvider(It.IsAny<string>()))
            .Returns(_mockProvider.Object);
            
        _controller = new ConversionController(_mockFactory.Object, _mockLogger.Object);
        
        // Set up controller context with mock HttpContext
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "test-user"),
            new Claim(ClaimTypes.Name, "Test User"),
            new Claim(ClaimTypes.Role, "User")
        }, "mock"));
        
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };
    }

    [Fact]
    public async Task ConvertCurrency_ValidRequest_ReturnsOkResult()
    {
        // Arrange
        string from = "EUR";
        string to = "USD";
        decimal amount = 100;
        string provider = "Frankfurter";
        
        var expectedResult = new ConversionResult
        {
            FromCurrency = from,
            ToCurrency = to,
            Amount = amount,
            ConvertedAmount = 110,
            Rate = 1.1m,
            Date = DateTime.Today
        };
        
        _mockProvider.Setup(p => p.ConvertCurrencyAsync(
                from, to, amount, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _controller.ConvertCurrency(from, to, amount, provider);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var apiResponse = Assert.IsType<ApiResponse<ConversionResult>>(okResult.Value);
        Assert.True(apiResponse.Success);
        Assert.Equal(expectedResult, apiResponse.Data);
    }

    [Fact]
    public async Task ConvertCurrency_InvalidRequest_ReturnsBadRequest()
    {
        // Arrange
        string from = "INVALID";
        string to = "USD";
        decimal amount = 100;
        string provider = "Frankfurter";
        
        _mockProvider.Setup(p => p.ConvertCurrencyAsync(
                from, to, amount, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException($"Invalid currency: {from}"));

        // Act
        var result = await _controller.ConvertCurrency(from, to, amount, provider);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        var apiResponse = Assert.IsType<ApiResponse<ConversionResult>>(badRequestResult.Value);
        Assert.False(apiResponse.Success);
        Assert.Contains(from, apiResponse.Message);
    }
}