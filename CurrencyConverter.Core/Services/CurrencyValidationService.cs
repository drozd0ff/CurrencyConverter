using CurrencyConverter.Core.Interfaces;

namespace CurrencyConverter.Core.Services
{
    public class CurrencyValidationService : ICurrencyValidationService
    {
        private readonly HashSet<string> _restrictedCurrencies = new(StringComparer.OrdinalIgnoreCase)
        {
            "TRY", "PLN", "THB", "MXN"
        };

        private readonly HashSet<string> _validCurrencyCodes = new(StringComparer.OrdinalIgnoreCase)
        {
            "USD", "EUR", "GBP", "JPY", "CAD", "AUD", "CHF", "CNY", "HKD", "NZD", 
            "SEK", "KRW", "SGD", "NOK", "MXN", "INR", "RUB", "ZAR", "TRY", "BRL", 
            "TWD", "DKK", "PLN", "THB", "IDR", "HUF", "CZK", "ILS", "CLP", "PHP", 
            "AED", "COP", "SAR", "MYR", "RON"
        };

        public bool IsValidCurrency(string? currency)
        {
            return !string.IsNullOrWhiteSpace(currency) && _validCurrencyCodes.Contains(currency);
        }

        public bool IsRestrictedCurrency(string? currency)
        {
            return !string.IsNullOrWhiteSpace(currency) && _restrictedCurrencies.Contains(currency);
        }

        public bool AreValidCurrencies(params string[]? currencies)
        {
            return currencies != null && currencies.All(c => IsValidCurrency(c));
        }
    }
}