using CurrencyConverter.Core.Models;

namespace CurrencyConverter.Core.Interfaces;

public interface IExchangeRateProvider
{
    Task<ExchangeRate> GetLatestRatesAsync(string baseCurrency, CancellationToken cancellationToken = default);
    Task<ConversionResult> ConvertCurrencyAsync(string fromCurrency, string toCurrency, decimal amount, CancellationToken cancellationToken = default);
    Task<HistoricalRatesResult> GetHistoricalRatesAsync(DateTime startDate, DateTime endDate, string baseCurrency, int page, int pageSize, CancellationToken cancellationToken = default);
}