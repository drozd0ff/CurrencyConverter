using System.Net.Http.Json;
using System.Text.Json;
using CurrencyConverter.Core.Interfaces;
using CurrencyConverter.Core.Models;
using Microsoft.Extensions.Logging;

namespace CurrencyConverter.Infrastructure.ExternalServices
{
    public class FrankfurterApiClient : IFrankfurterApiClient
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<FrankfurterApiClient> _logger;
        private readonly JsonSerializerOptions _jsonOptions;

        public FrankfurterApiClient(HttpClient httpClient, ILogger<FrankfurterApiClient> logger)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
        }

        public async Task<ExchangeRate> GetLatestRatesAsync(string baseCurrency, CancellationToken cancellationToken = default)
        {
            try
            {
                string requestUri = $"latest?base={baseCurrency}";
                _logger.LogInformation("Requesting latest rates from Frankfurter API with base currency {BaseCurrency}", baseCurrency);

                var response = await _httpClient.GetFromJsonAsync<ExchangeRate>(requestUri, _jsonOptions, cancellationToken) 
                    ?? throw new InvalidOperationException($"Failed to fetch latest rates for base currency {baseCurrency}");
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching latest rates from Frankfurter API for base currency {BaseCurrency}", baseCurrency);
                throw;
            }
        }

        public async Task<ExchangeRate> GetRateForDateAsync(DateTime date, string baseCurrency, CancellationToken cancellationToken = default)
        {
            try
            {
                string formattedDate = date.ToString("yyyy-MM-dd");
                string requestUri = $"{formattedDate}?base={baseCurrency}";

                _logger.LogInformation("Requesting rates for date {Date} from Frankfurter API with base currency {BaseCurrency}", formattedDate, baseCurrency);

                var response = await _httpClient.GetFromJsonAsync<ExchangeRate>(requestUri, _jsonOptions, cancellationToken)
                    ?? throw new InvalidOperationException($"Failed to fetch rates for date {formattedDate} with base currency {baseCurrency}");
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching rates for date {Date} from Frankfurter API with base currency {BaseCurrency}", date.ToString("yyyy-MM-dd"), baseCurrency);
                throw;
            }
        }

        public async Task<Dictionary<DateTime, Dictionary<string, decimal>>> GetHistoricalRatesAsync(DateTime startDate, DateTime endDate, string baseCurrency, CancellationToken cancellationToken = default)
        {
            try
            {
                string formattedStartDate = startDate.ToString("yyyy-MM-dd");
                string formattedEndDate = endDate.ToString("yyyy-MM-dd");
                string requestUri = $"{formattedStartDate}..{formattedEndDate}?base={baseCurrency}";

                _logger.LogInformation("Requesting historical rates from {StartDate} to {EndDate} from Frankfurter API with base currency {BaseCurrency}",
                    formattedStartDate, formattedEndDate, baseCurrency);

                var response = await _httpClient.GetAsync(requestUri, cancellationToken);
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                var jsonDoc = JsonDocument.Parse(content);

                var result = new Dictionary<DateTime, Dictionary<string, decimal>>();

                var ratesElement = jsonDoc.RootElement.GetProperty("rates");
                foreach (var dateProperty in ratesElement.EnumerateObject())
                {
                    if (DateTime.TryParse(dateProperty.Name, out var date))
                    {
                        var ratesForDate = new Dictionary<string, decimal>();
                        foreach (var rateProperty in dateProperty.Value.EnumerateObject())
                        {
                            if (decimal.TryParse(rateProperty.Value.ToString(), out var rate))
                            {
                                ratesForDate.Add(rateProperty.Name, rate);
                            }
                        }
                        result.Add(date, ratesForDate);
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching historical rates from {StartDate} to {EndDate} from Frankfurter API with base currency {BaseCurrency}",
                    startDate.ToString("yyyy-MM-dd"), endDate.ToString("yyyy-MM-dd"), baseCurrency);
                throw;
            }
        }
    }
}