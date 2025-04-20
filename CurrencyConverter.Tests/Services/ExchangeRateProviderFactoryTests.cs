using CurrencyConverter.Core.Interfaces;
using CurrencyConverter.Infrastructure.ExternalServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CurrencyConverter.Tests.Services;

public class ExchangeRateProviderFactoryTests
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ExchangeRateProviderFactory _factory;
    private readonly Mock<IFrankfurterApiClient> _mockApiClient;
    private readonly Mock<ICacheService> _mockCacheService;
    private readonly Mock<ICurrencyValidationService> _mockCurrencyValidationService;
    private readonly Mock<ILogger<FrankfurterExchangeRateProvider>> _mockLogger;

    public ExchangeRateProviderFactoryTests()
    {
        // Create mock dependencies
        _mockApiClient = new Mock<IFrankfurterApiClient>();
        _mockCacheService = new Mock<ICacheService>();
        _mockCurrencyValidationService = new Mock<ICurrencyValidationService>();
        _mockLogger = new Mock<ILogger<FrankfurterExchangeRateProvider>>();
        
        // Create a service collection and register the required services
        var services = new ServiceCollection();
        
        // Register the mock dependencies
        services.AddSingleton(_mockApiClient.Object);
        services.AddSingleton(_mockCacheService.Object);
        services.AddSingleton(_mockCurrencyValidationService.Object);
        services.AddSingleton(_mockLogger.Object);
        
        // Register the real FrankfurterExchangeRateProvider (not mocking it)
        services.AddTransient<FrankfurterExchangeRateProvider>();
        
        // Build the service provider
        _serviceProvider = services.BuildServiceProvider();
        
        // Create the factory with the service provider
        _factory = new ExchangeRateProviderFactory(_serviceProvider);
    }

    [Fact]
    public void Constructor_WithNullServiceProvider_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new ExchangeRateProviderFactory(null!));
    }

    [Fact]
    public void CreateProvider_WithFrankfurterProvider_ReturnsInstance()
    {
        // Act
        var provider = _factory.CreateProvider("frankfurter");

        // Assert
        Assert.NotNull(provider);
        Assert.IsType<FrankfurterExchangeRateProvider>(provider);
    }

    [Fact]
    public void CreateProvider_WithCaseInsensitiveProvider_ReturnsInstance()
    {
        // Act
        var provider = _factory.CreateProvider("FrAnKfUrTeR");

        // Assert
        Assert.NotNull(provider);
        Assert.IsType<FrankfurterExchangeRateProvider>(provider);
    }

    [Fact]
    public void CreateProvider_WithDefaultParameter_ReturnsFrankfurterProvider()
    {
        // Act
        var provider = _factory.CreateProvider();

        // Assert
        Assert.NotNull(provider);
        Assert.IsType<FrankfurterExchangeRateProvider>(provider);
    }

    [Fact]
    public void CreateProvider_WithUnsupportedProvider_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => _factory.CreateProvider("unsupported"));
        Assert.Contains("Unsupported exchange rate provider", exception.Message);
    }
}