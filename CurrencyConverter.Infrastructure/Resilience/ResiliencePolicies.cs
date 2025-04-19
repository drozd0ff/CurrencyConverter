using System.Net;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;

namespace CurrencyConverter.Infrastructure.Resilience
{
    public class ResiliencePolicies
    {
        private readonly ILogger<ResiliencePolicies> _logger;
        private readonly AsyncRetryPolicy<HttpResponseMessage> _retryPolicy;
        private readonly AsyncCircuitBreakerPolicy<HttpResponseMessage> _circuitBreakerPolicy;

        public ResiliencePolicies(ILogger<ResiliencePolicies> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Create retry policy with exponential backoff
            _retryPolicy = Policy<HttpResponseMessage>
                .Handle<HttpRequestException>()
                .Or<TimeoutException>()
                .OrResult(r =>
                {
                    return r.StatusCode == HttpStatusCode.TooManyRequests ||
                           r.StatusCode == HttpStatusCode.RequestTimeout ||
                           (int)r.StatusCode >= 500;
                })
                .WaitAndRetryAsync(
                    retryCount: 3,
                    sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    onRetry: (outcome, timespan, retryAttempt, context) =>
                    {
                        _logger.LogWarning(
                            "Retrying request after {RetryAttempt} attempts. Delay: {Delay}ms. " +
                            "Exception: {Exception}",
                            retryAttempt, timespan.TotalMilliseconds,
                            outcome.Exception?.Message ?? $"Status code: {outcome.Result?.StatusCode}");
                    });

            // Create circuit breaker policy
            _circuitBreakerPolicy = Policy<HttpResponseMessage>
                .Handle<HttpRequestException>()
                .Or<TimeoutException>()
                .OrResult(r => (int)r.StatusCode >= 500)
                .AdvancedCircuitBreakerAsync(
                    failureThreshold: 0.5, // Break on 50% failure rate
                    samplingDuration: TimeSpan.FromMinutes(1),
                    minimumThroughput: 10, // Minimum number of actions before circuit can break
                    durationOfBreak: TimeSpan.FromSeconds(30),
                    onBreak: (outcome, state, timespan, context) =>
                    {
                        _logger.LogWarning(
                            "Circuit breaker opened for {DurationOfBreak}ms. " +
                            "Exception: {Exception}",
                            timespan.TotalMilliseconds,
                            outcome.Exception?.Message ?? $"Status code: {outcome.Result?.StatusCode}");
                    },
                    onReset: context =>
                    {
                        _logger.LogInformation("Circuit breaker reset");
                    },
                    onHalfOpen: () =>
                    {
                        _logger.LogInformation("Circuit breaker half-open");
                    });
        }

        public IAsyncPolicy<HttpResponseMessage> GetCombinedPolicy()
        {
            return Policy.WrapAsync(_retryPolicy, _circuitBreakerPolicy);
        }
    }
}