using CurrencyConverter.Core.Models;

namespace CurrencyConverter.Core.Interfaces;

public interface IFrankfurterApiClient
{
    Task<ExchangeRate> GetLatestRatesAsync(string baseCurrency, CancellationToken cancellationToken = default);
    Task<ExchangeRate> GetRateForDateAsync(DateTime date, string baseCurrency, CancellationToken cancellationToken = default);
    Task<Dictionary<DateTime, Dictionary<string, decimal>>> GetHistoricalRatesAsync(DateTime startDate, DateTime endDate, string baseCurrency, CancellationToken cancellationToken = default);
}