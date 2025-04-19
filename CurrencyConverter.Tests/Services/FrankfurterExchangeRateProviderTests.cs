using CurrencyConverter.Core.Interfaces;
using CurrencyConverter.Core.Models;
using CurrencyConverter.Infrastructure.ExternalServices;
using Microsoft.Extensions.Logging;

namespace CurrencyConverter.Tests.Services;

public class FrankfurterExchangeRateProviderTests
{
    private readonly Mock<IFrankfurterApiClient> _mockApiClient;
    private readonly Mock<ICacheService> _mockCacheService;
    private readonly Mock<ICurrencyValidationService> _mockValidationService;
    private readonly Mock<ILogger<FrankfurterExchangeRateProvider>> _mockLogger;
    private readonly FrankfurterExchangeRateProvider _provider;

    public FrankfurterExchangeRateProviderTests()
    {
        _mockApiClient = new Mock<IFrankfurterApiClient>();
        _mockCacheService = new Mock<ICacheService>();
        _mockValidationService = new Mock<ICurrencyValidationService>();
        _mockLogger = new Mock<ILogger<FrankfurterExchangeRateProvider>>();

        _provider = new FrankfurterExchangeRateProvider(
            _mockApiClient.Object,
            _mockCacheService.Object,
            _mockValidationService.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task GetLatestRatesAsync_ValidBaseCurrency_ReturnsExchangeRates()
    {
        // Arrange
        string baseCurrency = "EUR";
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

        _mockValidationService.Setup(x => x.IsValidCurrency(baseCurrency)).Returns(true);
        _mockValidationService.Setup(x => x.IsRestrictedCurrency(baseCurrency)).Returns(false);
        _mockValidationService.Setup(x => x.IsRestrictedCurrency("USD")).Returns(false);
        _mockValidationService.Setup(x => x.IsRestrictedCurrency("GBP")).Returns(false);
        _mockValidationService.Setup(x => x.IsRestrictedCurrency("JPY")).Returns(false);

        _mockCacheService
            .Setup(x => x.GetOrCreateAsync(
                It.IsAny<string>(),
                It.IsAny<Func<Task<ExchangeRate>>>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedRates);

        // Act
        var result = await _provider.GetLatestRatesAsync(baseCurrency);

        // Assert
        Assert.Equal(baseCurrency, result.Base);
        Assert.Equal(3, result.Rates.Count);
        Assert.Equal(1.1m, result.Rates["USD"]);
    }

    [Fact]
    public async Task GetLatestRatesAsync_InvalidCurrency_ThrowsArgumentException()
    {
        // Arrange
        string baseCurrency = "INVALID";

        _mockValidationService.Setup(x => x.IsValidCurrency(baseCurrency)).Returns(false);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _provider.GetLatestRatesAsync(baseCurrency));

        Assert.Contains(baseCurrency, exception.Message);
    }

    [Fact]
    public async Task GetLatestRatesAsync_RestrictedCurrency_ThrowsArgumentException()
    {
        // Arrange
        string baseCurrency = "TRY";

        _mockValidationService.Setup(x => x.IsValidCurrency(baseCurrency)).Returns(true);
        _mockValidationService.Setup(x => x.IsRestrictedCurrency(baseCurrency)).Returns(true);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _provider.GetLatestRatesAsync(baseCurrency));

        Assert.Contains("Restricted", exception.Message);
    }

    [Fact]
    public async Task ConvertCurrencyAsync_ValidCurrencies_ReturnsConversionResult()
    {
        // Arrange
        string fromCurrency = "EUR";
        string toCurrency = "USD";
        decimal amount = 100m;
        decimal rate = 1.1m;

        var exchangeRate = new ExchangeRate
        {
            Base = fromCurrency,
            Date = DateTime.Today,
            Rates = new Dictionary<string, decimal> { { toCurrency, rate } }
        };

        _mockValidationService.Setup(x => x.IsValidCurrency(fromCurrency)).Returns(true);
        _mockValidationService.Setup(x => x.IsValidCurrency(toCurrency)).Returns(true);
        _mockValidationService.Setup(x => x.IsRestrictedCurrency(fromCurrency)).Returns(false);
        _mockValidationService.Setup(x => x.IsRestrictedCurrency(toCurrency)).Returns(false);

        _mockCacheService
            .Setup(x => x.GetOrCreateAsync(
                It.IsAny<string>(),
                It.IsAny<Func<Task<ExchangeRate>>>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(exchangeRate);

        // Act
        var result = await _provider.ConvertCurrencyAsync(fromCurrency, toCurrency, amount);

        // Assert
        Assert.Equal(fromCurrency, result.FromCurrency);
        Assert.Equal(toCurrency, result.ToCurrency);
        Assert.Equal(amount, result.Amount);
        Assert.Equal(amount * rate, result.ConvertedAmount);
        Assert.Equal(rate, result.Rate);
    }

    [Theory]
    [InlineData("INVALID", "USD", 100)]
    [InlineData("EUR", "INVALID", 100)]
    public async Task ConvertCurrencyAsync_InvalidCurrency_ThrowsArgumentException(
        string fromCurrency, string toCurrency, decimal amount)
    {
        // Arrange
        _mockValidationService
            .Setup(x => x.IsValidCurrency(It.IsAny<string>()))
            .Returns<string>(currency => currency != "INVALID");

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _provider.ConvertCurrencyAsync(fromCurrency, toCurrency, amount));
    }

    [Theory]
    [InlineData("TRY", "USD", 100)]
    [InlineData("EUR", "PLN", 100)]
    public async Task ConvertCurrencyAsync_RestrictedCurrency_ThrowsArgumentException(
        string fromCurrency, string toCurrency, decimal amount)
    {
        // Arrange
        _mockValidationService.Setup(x => x.IsValidCurrency(It.IsAny<string>())).Returns(true);
        _mockValidationService
            .Setup(x => x.IsRestrictedCurrency(It.IsAny<string>()))
            .Returns<string>(currency => currency == "TRY" || currency == "PLN");

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _provider.ConvertCurrencyAsync(fromCurrency, toCurrency, amount));
    }

    [Fact]
    public async Task ConvertCurrencyAsync_ZeroAmount_ThrowsArgumentException()
    {
        // Arrange
        string fromCurrency = "EUR";
        string toCurrency = "USD";
        decimal amount = 0;

        _mockValidationService.Setup(x => x.IsValidCurrency(It.IsAny<string>())).Returns(true);
        _mockValidationService.Setup(x => x.IsRestrictedCurrency(It.IsAny<string>())).Returns(false);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _provider.ConvertCurrencyAsync(fromCurrency, toCurrency, amount));

        Assert.Contains("must be greater than zero", exception.Message);
    }

    [Fact]
    public async Task GetHistoricalRatesAsync_ValidParameters_ReturnsHistoricalRates()
    {
        // Arrange
        string baseCurrency = "EUR";
        DateTime startDate = new DateTime(2023, 1, 1);
        DateTime endDate = new DateTime(2023, 1, 10);
        int page = 1;
        int pageSize = 5;

        var historicalRates = new Dictionary<DateTime, Dictionary<string, decimal>>
        {
            { new DateTime(2023, 1, 1), new Dictionary<string, decimal> { { "USD", 1.1m } } },
            { new DateTime(2023, 1, 2), new Dictionary<string, decimal> { { "USD", 1.11m } } },
            { new DateTime(2023, 1, 3), new Dictionary<string, decimal> { { "USD", 1.12m } } },
            { new DateTime(2023, 1, 4), new Dictionary<string, decimal> { { "USD", 1.13m } } },
            { new DateTime(2023, 1, 5), new Dictionary<string, decimal> { { "USD", 1.14m } } },
            { new DateTime(2023, 1, 6), new Dictionary<string, decimal> { { "USD", 1.15m } } },
            { new DateTime(2023, 1, 7), new Dictionary<string, decimal> { { "USD", 1.16m } } },
        };

        _mockValidationService.Setup(x => x.IsValidCurrency(baseCurrency)).Returns(true);
        _mockValidationService.Setup(x => x.IsRestrictedCurrency(baseCurrency)).Returns(false);
        _mockValidationService.Setup(x => x.IsRestrictedCurrency("USD")).Returns(false);

        _mockCacheService
            .Setup(x => x.GetOrCreateAsync(
                It.IsAny<string>(),
                It.IsAny<Func<Task<Dictionary<DateTime, Dictionary<string, decimal>>>>>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(historicalRates);

        // Act
        var result = await _provider.GetHistoricalRatesAsync(
            startDate, endDate, baseCurrency, page, pageSize);

        // Assert
        Assert.Equal(baseCurrency, result.Base);
        Assert.Equal(startDate, result.StartDate);
        Assert.Equal(endDate, result.EndDate);
        Assert.Equal(5, result.Rates.Count);
        Assert.Equal(1, result.Pagination.CurrentPage);
        Assert.Equal(5, result.Pagination.PageSize);
        Assert.Equal(7, result.Pagination.TotalCount);
        Assert.Equal(2, result.Pagination.TotalPages);
    }
}