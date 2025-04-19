using CurrencyConverter.Core.Interfaces;
using CurrencyConverter.Core.Models;
using Microsoft.Extensions.Logging;

namespace CurrencyConverter.Infrastructure.ExternalServices
{
    public class FrankfurterExchangeRateProvider : IExchangeRateProvider
    {
        private readonly IFrankfurterApiClient _apiClient;
        private readonly ICacheService _cacheService;
        private readonly ICurrencyValidationService _currencyValidationService;
        private readonly ILogger<FrankfurterExchangeRateProvider> _logger;

        public FrankfurterExchangeRateProvider(
            IFrankfurterApiClient apiClient,
            ICacheService cacheService,
            ICurrencyValidationService currencyValidationService,
            ILogger<FrankfurterExchangeRateProvider> logger)
        {
            _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
            _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
            _currencyValidationService = currencyValidationService ?? throw new ArgumentNullException(nameof(currencyValidationService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<ExchangeRate> GetLatestRatesAsync(string baseCurrency, CancellationToken cancellationToken = default)
        {
            if (!_currencyValidationService.IsValidCurrency(baseCurrency))
            {
                throw new ArgumentException($"Invalid currency code: {baseCurrency}", nameof(baseCurrency));
            }

            if (_currencyValidationService.IsRestrictedCurrency(baseCurrency))
            {
                throw new ArgumentException($"Restricted currency code: {baseCurrency}", nameof(baseCurrency));
            }

            string cacheKey = $"latest_rates_{baseCurrency}";

            return await _cacheService.GetOrCreateAsync(cacheKey, async () =>
                {
                    var result = await _apiClient.GetLatestRatesAsync(baseCurrency, cancellationToken);

                    // Filter out restricted currencies
                    result.Rates = result.Rates
                        .Where(r => !_currencyValidationService.IsRestrictedCurrency(r.Key))
                        .ToDictionary(r => r.Key, r => r.Value);

                    return result;
                },
                TimeSpan.FromMinutes(30), // Cache for 30 minutes
                cancellationToken);
        }

        public async Task<ConversionResult> ConvertCurrencyAsync(string fromCurrency, string toCurrency, decimal amount, CancellationToken cancellationToken = default)
        {
            if (!_currencyValidationService.IsValidCurrency(fromCurrency))
            {
                throw new ArgumentException($"Invalid source currency code: {fromCurrency}", nameof(fromCurrency));
            }

            if (!_currencyValidationService.IsValidCurrency(toCurrency))
            {
                throw new ArgumentException($"Invalid target currency code: {toCurrency}", nameof(toCurrency));
            }

            if (_currencyValidationService.IsRestrictedCurrency(fromCurrency))
            {
                throw new ArgumentException($"Restricted source currency code: {fromCurrency}", nameof(fromCurrency));
            }

            if (_currencyValidationService.IsRestrictedCurrency(toCurrency))
            {
                throw new ArgumentException($"Restricted target currency code: {toCurrency}", nameof(toCurrency));
            }

            if (amount <= 0)
            {
                throw new ArgumentException("Amount must be greater than zero", nameof(amount));
            }

            // Get latest rates with the fromCurrency as base
            var latestRates = await GetLatestRatesAsync(fromCurrency, cancellationToken);

            if (!latestRates.Rates.TryGetValue(toCurrency, out decimal rate))
            {
                // If the target currency is the same as base currency
                if (string.Equals(fromCurrency, toCurrency, StringComparison.OrdinalIgnoreCase))
                {
                    rate = 1m;
                }
                else
                {
                    throw new InvalidOperationException($"Exchange rate not found for currency pair {fromCurrency}/{toCurrency}");
                }
            }

            decimal convertedAmount = amount * rate;

            return new ConversionResult
            {
                FromCurrency = fromCurrency,
                ToCurrency = toCurrency,
                Amount = amount,
                ConvertedAmount = convertedAmount,
                Rate = rate,
                Date = latestRates.Date
            };
        }

        public async Task<HistoricalRatesResult> GetHistoricalRatesAsync(DateTime startDate, DateTime endDate, string baseCurrency, int page, int pageSize, CancellationToken cancellationToken = default)
        {
            if (!_currencyValidationService.IsValidCurrency(baseCurrency))
            {
                throw new ArgumentException($"Invalid currency code: {baseCurrency}", nameof(baseCurrency));
            }

            if (_currencyValidationService.IsRestrictedCurrency(baseCurrency))
            {
                throw new ArgumentException($"Restricted currency code: {baseCurrency}", nameof(baseCurrency));
            }

            if (startDate > endDate)
            {
                throw new ArgumentException("Start date must be earlier than or equal to end date");
            }

            if (page < 1)
            {
                throw new ArgumentException("Page must be greater than or equal to 1", nameof(page));
            }

            if (pageSize < 1)
            {
                throw new ArgumentException("Page size must be greater than or equal to 1", nameof(pageSize));
            }

            // Use a fixed cache duration for historical data, as it doesn't change
            string cacheKey = $"historical_rates_{baseCurrency}_{startDate:yyyy-MM-dd}_{endDate:yyyy-MM-dd}";

            var historicalRatesDict = await _cacheService.GetOrCreateAsync(cacheKey, async () =>
                {
                    return await _apiClient.GetHistoricalRatesAsync(startDate, endDate, baseCurrency, cancellationToken);
                },
                TimeSpan.FromHours(24), // Cache for 24 hours
                cancellationToken);

            // Filter out restricted currencies and convert to list of ExchangeRate objects
            var allRates = historicalRatesDict
                .OrderBy(kvp => kvp.Key)
                .Select(kvp => new ExchangeRate
                {
                    Base = baseCurrency,
                    Date = kvp.Key,
                    Rates = kvp.Value.Where(r => !_currencyValidationService.IsRestrictedCurrency(r.Key))
                        .ToDictionary(r => r.Key, r => r.Value)
                })
                .ToList();

            // Calculate pagination
            int totalCount = allRates.Count;
            int totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            // Apply pagination
            var pagedRates = allRates
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var paginationMetadata = new PaginationMetadata
            {
                CurrentPage = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = totalPages
            };

            return new HistoricalRatesResult
            {
                Base = baseCurrency,
                StartDate = startDate,
                EndDate = endDate,
                Rates = pagedRates,
                Pagination = paginationMetadata
            };
        }
    }
}