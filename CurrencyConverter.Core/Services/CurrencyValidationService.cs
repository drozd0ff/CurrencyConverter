using CurrencyConverter.Core.Interfaces;

namespace CurrencyConverter.Core.Services
{
    public class CurrencyValidationService : ICurrencyValidationService
    {
        private readonly HashSet<string> _restrictedCurrencies = new(StringComparer.OrdinalIgnoreCase)
        {
            "TRY", "PLN", "THB", "MXN"
        };

        public bool IsValidCurrency(string? currency)
        {
            return !string.IsNullOrWhiteSpace(currency);
        }

        public bool IsRestrictedCurrency(string? currency)
        {
            return !string.IsNullOrWhiteSpace(currency) && _restrictedCurrencies.Contains(currency);
        }

        public bool AreValidCurrencies(params string[]? currencies)
        {
            return currencies != null && currencies.All(c => !string.IsNullOrWhiteSpace(c));
        }
    }
}