namespace CurrencyConverter.Core.Interfaces;

public interface ICurrencyValidationService
{
    bool IsValidCurrency(string? currency);
    bool IsRestrictedCurrency(string? currency);
    bool AreValidCurrencies(params string[]? currencies);
}