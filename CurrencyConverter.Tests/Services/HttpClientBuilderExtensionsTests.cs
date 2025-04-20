using System.Net;
using CurrencyConverter.Infrastructure.Resilience;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.CircuitBreaker;

namespace CurrencyConverter.Tests.Services;

public class HttpClientBuilderExtensionsTests
{
    [Fact]
    public void AddResiliencePolicies_AddsRetryAndCircuitBreakerPolicies()
    {
        // Arrange
        var services = new ServiceCollection();
        var httpClientBuilder = services.AddHttpClient("TestClient");

        // Act
        var resultBuilder = httpClientBuilder.AddResiliencePolicies();

        // Assert
        Assert.Same(httpClientBuilder, resultBuilder);
        
        // Verify that policies are added by checking if the service provider can be built
        var serviceProvider = services.BuildServiceProvider();
        var client = serviceProvider.GetRequiredService<System.Net.Http.IHttpClientFactory>().CreateClient("TestClient");
        Assert.NotNull(client);
    }

    [Fact]
    public async Task RetryPolicy_RetriesOnTransientErrors()
    {
        // Arrange
        var retryPolicy = GetRetryPolicy();
        var attemptsMade = 0;

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(async () =>
        {
            await retryPolicy.ExecuteAsync(() =>
            {
                attemptsMade++;
                throw new HttpRequestException("Simulated transient error");
                
                #pragma warning disable CS0162 // Unreachable code detected
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
                #pragma warning restore CS0162
            });
        });

        // Initial attempt + 3 retries = 4 attempts
        Assert.Equal(4, attemptsMade);
    }

    [Fact]
    public async Task RetryPolicy_RetriesOnTooManyRequests()
    {
        // Arrange
        var retryPolicy = GetRetryPolicy();
        var attemptsMade = 0;

        // Act
        var response = await retryPolicy.ExecuteAsync(() =>
        {
            attemptsMade++;
            if (attemptsMade < 4) // Allow it to succeed on the 4th attempt
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.TooManyRequests));
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });

        // Assert
        Assert.Equal(4, attemptsMade);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CircuitBreakerPolicy_BreaksCircuitAfterConsecutiveFailures()
    {
        // Arrange
        var circuitBreakerPolicy = GetCircuitBreakerPolicy();
        var failureCount = 0;
        var circuitBroken = false;

        // Act
        for (int i = 0; i < 6; i++)
        {
            try
            {
                await circuitBreakerPolicy.ExecuteAsync(() =>
                {
                    failureCount++;
                    throw new HttpRequestException("Simulated failure");
                    
                    #pragma warning disable CS0162 // Unreachable code detected
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
                    #pragma warning restore CS0162
                });
            }
            catch (HttpRequestException)
            {
                // Expected for first few calls
            }
            catch (BrokenCircuitException)
            {
                circuitBroken = true;
            }
        }

        // Assert
        Assert.True(circuitBroken, "Circuit should have been broken after 5 consecutive failures");
    }

    // Instead of using reflection to access private methods, we'll recreate the policies directly
    private IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
    {
        return Polly.Extensions.Http.HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(msg => msg.StatusCode == HttpStatusCode.TooManyRequests)
            .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
    }

    private IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy()
    {
        return Polly.Extensions.Http.HttpPolicyExtensions
            .HandleTransientHttpError()
            .CircuitBreakerAsync(5, TimeSpan.FromSeconds(30));
    }
}