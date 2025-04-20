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
    public async Task ConvertCurrencyAsync_SameCurrency_ReturnsIdenticalAmount()
    {
        // Arrange
        string currency = "EUR";
        decimal amount = 100m;

        _mockValidationService.Setup(x => x.IsValidCurrency(currency)).Returns(true);
        _mockValidationService.Setup(x => x.IsRestrictedCurrency(currency)).Returns(false);

        var exchangeRate = new ExchangeRate
        {
            Base = currency,
            Date = DateTime.Today,
            Rates = new Dictionary<string, decimal>() // Empty rates, as same currency won't be in rates
        };

        _mockCacheService
            .Setup(x => x.GetOrCreateAsync(
                It.IsAny<string>(),
                It.IsAny<Func<Task<ExchangeRate>>>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(exchangeRate);

        // Act
        var result = await _provider.ConvertCurrencyAsync(currency, currency, amount);

        // Assert
        Assert.Equal(currency, result.FromCurrency);
        Assert.Equal(currency, result.ToCurrency);
        Assert.Equal(amount, result.Amount);
        Assert.Equal(amount, result.ConvertedAmount); // Same amount for same currency
        Assert.Equal(1m, result.Rate); // Rate should be 1 for same currency
    }

    [Fact]
    public async Task ConvertCurrencyAsync_RateNotFound_ThrowsInvalidOperationException()
    {
        // Arrange
        string fromCurrency = "EUR";
        string toCurrency = "JPY";
        decimal amount = 100m;

        _mockValidationService.Setup(x => x.IsValidCurrency(It.IsAny<string>())).Returns(true);
        _mockValidationService.Setup(x => x.IsRestrictedCurrency(It.IsAny<string>())).Returns(false);

        var exchangeRate = new ExchangeRate
        {
            Base = fromCurrency,
            Date = DateTime.Today,
            Rates = new Dictionary<string, decimal>
            {
                { "USD", 1.1m } // JPY not included in rates
            }
        };

        _mockCacheService
            .Setup(x => x.GetOrCreateAsync(
                It.IsAny<string>(),
                It.IsAny<Func<Task<ExchangeRate>>>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(exchangeRate);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => 
            _provider.ConvertCurrencyAsync(fromCurrency, toCurrency, amount));
        
        Assert.Contains($"{fromCurrency}/{toCurrency}", exception.Message);
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

    [Fact]
    public async Task GetHistoricalRatesAsync_InvalidParameters_ThrowsArgumentException()
    {
        // Arrange & Act & Assert
        
        // Test invalid currency
        _mockValidationService.Setup(x => x.IsValidCurrency("INVALID")).Returns(false);
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _provider.GetHistoricalRatesAsync(DateTime.Today, DateTime.Today, "INVALID", 1, 10));
            
        // Test restricted currency
        _mockValidationService.Setup(x => x.IsValidCurrency("TRY")).Returns(true);
        _mockValidationService.Setup(x => x.IsRestrictedCurrency("TRY")).Returns(true);
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _provider.GetHistoricalRatesAsync(DateTime.Today, DateTime.Today, "TRY", 1, 10));
            
        // Test invalid date range
        var futureDate = DateTime.Today.AddDays(1);
        var pastDate = DateTime.Today.AddDays(-1);
        _mockValidationService.Setup(x => x.IsValidCurrency("EUR")).Returns(true);
        _mockValidationService.Setup(x => x.IsRestrictedCurrency("EUR")).Returns(false);
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _provider.GetHistoricalRatesAsync(futureDate, pastDate, "EUR", 1, 10));
            
        // Test invalid page
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _provider.GetHistoricalRatesAsync(DateTime.Today, DateTime.Today.AddDays(1), "EUR", 0, 10));
            
        // Test invalid pageSize
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _provider.GetHistoricalRatesAsync(DateTime.Today, DateTime.Today.AddDays(1), "EUR", 1, 0));
    }

    [Fact]
    public async Task GetHistoricalRatesAsync_SecondPage_ReturnsCorrectPagedResults()
    {
        // Arrange
        string baseCurrency = "EUR";
        DateTime startDate = new DateTime(2023, 1, 1);
        DateTime endDate = new DateTime(2023, 1, 10);
        int page = 2;
        int pageSize = 3;

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
        Assert.Equal(3, result.Rates.Count); // Second page of 3 items
        Assert.Equal(2, result.Pagination.CurrentPage);
        Assert.Equal(3, result.Pagination.PageSize);
        Assert.Equal(7, result.Pagination.TotalCount);
        Assert.Equal(3, result.Pagination.TotalPages);
        Assert.True(result.Pagination.HasPrevious); // Page 2 should have previous
        Assert.True(result.Pagination.HasNext); // With 7 items, 3 per page, page 2 of 3 should have next
        
        // Check that we have dates 4, 5, and 6 (second page of results)
        Assert.Contains(result.Rates, r => r.Date == new DateTime(2023, 1, 4));
        Assert.Contains(result.Rates, r => r.Date == new DateTime(2023, 1, 5));
        Assert.Contains(result.Rates, r => r.Date == new DateTime(2023, 1, 6));
    }

    [Fact]
    public async Task GetHistoricalRatesAsync_FiltersRestrictedCurrencies()
    {
        // Arrange
        string baseCurrency = "EUR";
        DateTime startDate = new DateTime(2023, 1, 1);
        DateTime endDate = new DateTime(2023, 1, 2);
        int page = 1;
        int pageSize = 10;

        // Set up a mix of allowed and restricted currencies
        var historicalRates = new Dictionary<DateTime, Dictionary<string, decimal>>
        {
            { new DateTime(2023, 1, 1), new Dictionary<string, decimal> {
                { "USD", 1.1m },
                { "GBP", 0.85m },
                { "TRY", 20.5m }, // Restricted currency
                { "JPY", 130.0m }
            }}
        };

        _mockValidationService.Setup(x => x.IsValidCurrency(baseCurrency)).Returns(true);
        _mockValidationService.Setup(x => x.IsRestrictedCurrency(baseCurrency)).Returns(false);
        
        // Set up which currencies are restricted
        _mockValidationService.Setup(x => x.IsRestrictedCurrency("USD")).Returns(false);
        _mockValidationService.Setup(x => x.IsRestrictedCurrency("GBP")).Returns(false);
        _mockValidationService.Setup(x => x.IsRestrictedCurrency("TRY")).Returns(true); // Restricted
        _mockValidationService.Setup(x => x.IsRestrictedCurrency("JPY")).Returns(false);

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
        Assert.Single(result.Rates);
        
        // Verify that TRY is filtered out from rates
        var ratesForJan1 = result.Rates.First();
        Assert.Equal(3, ratesForJan1.Rates.Count); // Should have 3 currencies (TRY filtered out)
        Assert.Contains("USD", ratesForJan1.Rates.Keys);
        Assert.Contains("GBP", ratesForJan1.Rates.Keys);
        Assert.Contains("JPY", ratesForJan1.Rates.Keys);
        Assert.DoesNotContain("TRY", ratesForJan1.Rates.Keys);
    }

    [Fact]
    public void Constructor_NullDependencies_ThrowsArgumentNullException()
    {
        // Arrange
        var validApiClient = _mockApiClient.Object;
        var validCacheService = _mockCacheService.Object;
        var validValidationService = _mockValidationService.Object;
        var validLogger = _mockLogger.Object;

        // Act & Assert - Test all constructor parameters
        Assert.Throws<ArgumentNullException>(() => new FrankfurterExchangeRateProvider(
            null!,
            validCacheService,
            validValidationService,
            validLogger
        ));

        Assert.Throws<ArgumentNullException>(() => new FrankfurterExchangeRateProvider(
            validApiClient,
            null!,
            validValidationService,
            validLogger
        ));

        Assert.Throws<ArgumentNullException>(() => new FrankfurterExchangeRateProvider(
            validApiClient,
            validCacheService,
            null!,
            validLogger
        ));

        Assert.Throws<ArgumentNullException>(() => new FrankfurterExchangeRateProvider(
            validApiClient,
            validCacheService,
            validValidationService,
            null!
        ));
    }
}