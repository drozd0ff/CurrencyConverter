namespace CurrencyConverter.Core.Interfaces;

public interface IExchangeRateProviderFactory
{
    IExchangeRateProvider CreateProvider(string providerName = "Frankfurter");
}