using CurrencyConverter.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace CurrencyConverter.Infrastructure.ExternalServices;

public class ExchangeRateProviderFactory : IExchangeRateProviderFactory
{
    private readonly IServiceProvider _serviceProvider;

    public ExchangeRateProviderFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    public IExchangeRateProvider CreateProvider(string providerName = "Frankfurter")
    {
        return providerName.ToLowerInvariant() switch
        {
            "frankfurter" => _serviceProvider.GetRequiredService<FrankfurterExchangeRateProvider>(),
            _ => throw new ArgumentException($"Unsupported exchange rate provider: {providerName}", nameof(providerName))
        };
    }
}