using CurrencyConverter.API.Controllers;
using CurrencyConverter.Core.Interfaces;
using CurrencyConverter.Core.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace CurrencyConverter.Tests.Controllers;

public class ExchangeRatesControllerTests
{
    private readonly Mock<IExchangeRateProviderFactory> _mockFactory;
    private readonly Mock<IExchangeRateProvider> _mockProvider;
    private readonly Mock<ILogger<ExchangeRatesController>> _mockLogger;
    private readonly ExchangeRatesController _controller;

    public ExchangeRatesControllerTests()
    {
        _mockFactory = new Mock<IExchangeRateProviderFactory>();
        _mockProvider = new Mock<IExchangeRateProvider>();
        _mockLogger = new Mock<ILogger<ExchangeRatesController>>();
        
        _mockFactory.Setup(f => f.CreateProvider(It.IsAny<string>()))
            .Returns(_mockProvider.Object);
            
        _controller = new ExchangeRatesController(_mockFactory.Object, _mockLogger.Object);
        
        // Set up controller context with mock HttpContext
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "test-user"),
            new Claim(ClaimTypes.Name, "Test User"),
            new Claim(ClaimTypes.Role, "Admin")
        }, "mock"));
        
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };
    }

    [Fact]
    public async Task GetLatestRates_ValidRequest_ReturnsOkResult()
    {
        // Arrange
        string baseCurrency = "EUR";
        string provider = "Frankfurter";
        
        var expectedRates = new ExchangeRate
        {
            Base = baseCurrency,
            Date = DateTime.Today,
            Rates = new Dictionary<string, decimal>
            {
                { "USD", 1.1m },
                { "GBP", 0.9m },
                { "JPY", 130.0m }
            }
        };
        
        _mockProvider.Setup(p => p.GetLatestRatesAsync(
                baseCurrency, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedRates);

        // Act
        var result = await _controller.GetLatestRates(baseCurrency, provider);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var apiResponse = Assert.IsType<ApiResponse<ExchangeRate>>(okResult.Value);
        Assert.True(apiResponse.Success);
        Assert.Equal(expectedRates, apiResponse.Data);
    }

    [Fact]
    public async Task GetLatestRates_InvalidCurrency_ReturnsBadRequest()
    {
        // Arrange
        string baseCurrency = "INVALID";
        string provider = "Frankfurter";
        
        _mockProvider.Setup(p => p.GetLatestRatesAsync(
                baseCurrency, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException($"Invalid currency code: {baseCurrency}"));

        // Act
        var result = await _controller.GetLatestRates(baseCurrency, provider);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        var apiResponse = Assert.IsType<ApiResponse<ExchangeRate>>(badRequestResult.Value);
        Assert.False(apiResponse.Success);
        Assert.Contains(baseCurrency, apiResponse.Message);
    }

    [Fact]
    public async Task GetHistoricalRates_ValidRequest_ReturnsOkResultWithPagination()
    {
        // Arrange
        DateTime from = new DateTime(2023, 1, 1);
        DateTime to = new DateTime(2023, 1, 10);
        string baseCurrency = "EUR";
        int page = 1;
        int pageSize = 5;
        string provider = "Frankfurter";
        
        var expectedResult = new HistoricalRatesResult
        {
            Base = baseCurrency,
            StartDate = from,
            EndDate = to,
            Rates = new List<ExchangeRate>
            {
                new ExchangeRate
                {
                    Base = baseCurrency,
                    Date = new DateTime(2023, 1, 1),
                    Rates = new Dictionary<string, decimal> { { "USD", 1.1m } }
                },
                // Additional rates would be here in a real example
            },
            Pagination = new PaginationMetadata
            {
                CurrentPage = page,
                PageSize = pageSize,
                TotalCount = 10,
                TotalPages = 2
            }
        };
        
        _mockProvider.Setup(p => p.GetHistoricalRatesAsync(
                from, to, baseCurrency, page, pageSize, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _controller.GetHistoricalRates(from, to, baseCurrency, page, pageSize, provider);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var apiResponse = Assert.IsType<ApiResponse<HistoricalRatesResult>>(okResult.Value);
        Assert.True(apiResponse.Success);
        Assert.Equal(expectedResult, apiResponse.Data);
        Assert.Equal(page, apiResponse.Data.Pagination.CurrentPage);
        Assert.Equal(pageSize, apiResponse.Data.Pagination.PageSize);
    }

    [Fact]
    public async Task GetHistoricalRates_InvalidDateRange_ReturnsBadRequest()
    {
        // Arrange
        DateTime from = new DateTime(2023, 1, 10);
        DateTime to = new DateTime(2023, 1, 1); // Invalid: end date before start date
        string baseCurrency = "EUR";
        int page = 1;
        int pageSize = 5;
        string provider = "Frankfurter";
        
        _mockProvider.Setup(p => p.GetHistoricalRatesAsync(
                from, to, baseCurrency, page, pageSize, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("Start date must be earlier than or equal to end date"));

        // Act
        var result = await _controller.GetHistoricalRates(from, to, baseCurrency, page, pageSize, provider);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        var apiResponse = Assert.IsType<ApiResponse<HistoricalRatesResult>>(badRequestResult.Value);
        Assert.False(apiResponse.Success);
        Assert.Contains("date", apiResponse.Message);
    }
}