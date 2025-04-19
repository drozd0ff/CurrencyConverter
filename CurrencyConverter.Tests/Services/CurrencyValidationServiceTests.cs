using CurrencyConverter.Core.Services;

#nullable enable
namespace CurrencyConverter.Tests.Services;

public class CurrencyValidationServiceTests
{
    private readonly CurrencyValidationService _validationService;

    public CurrencyValidationServiceTests()
    {
        _validationService = new CurrencyValidationService();
    }

    [Theory]
    [InlineData("USD", true)]
    [InlineData("EUR", true)]
    [InlineData("JPY", true)]
    [InlineData("GBP", true)]
    [InlineData("ABC", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsValidCurrency_ReturnsExpectedResult(string? currency, bool expected)
    {
        // Act
        var result = _validationService.IsValidCurrency(currency);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("TRY", true)]
    [InlineData("PLN", true)]
    [InlineData("THB", true)]
    [InlineData("MXN", true)]
    [InlineData("USD", false)]
    [InlineData("EUR", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsRestrictedCurrency_ReturnsExpectedResult(string? currency, bool expected)
    {
        // Act
        var result = _validationService.IsRestrictedCurrency(currency);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void AreValidCurrencies_WithValidCurrencies_ReturnsTrue()
    {
        // Arrange
        string[] currencies = { "USD", "EUR", "JPY" };

        // Act
        var result = _validationService.AreValidCurrencies(currencies);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void AreValidCurrencies_WithInvalidCurrency_ReturnsFalse()
    {
        // Arrange
        string[] currencies = { "USD", "INVALID", "JPY" };

        // Act
        var result = _validationService.AreValidCurrencies(currencies);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void AreValidCurrencies_WithNullArray_ReturnsFalse()
    {
        // Act
        var result = _validationService.AreValidCurrencies(null);

        // Assert
        Assert.False(result);
    }
}