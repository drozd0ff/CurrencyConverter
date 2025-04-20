using System.Net;
using CurrencyConverter.Infrastructure.Resilience;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;

namespace CurrencyConverter.Tests.Services;

public class ResiliencePoliciesTests
{
    private readonly Mock<ILogger<ResiliencePolicies>> _mockLogger;
    private readonly ResiliencePolicies _policies;

    public ResiliencePoliciesTests()
    {
        _mockLogger = new Mock<ILogger<ResiliencePolicies>>();
        _policies = new ResiliencePolicies(_mockLogger.Object);
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new ResiliencePolicies(null!));
    }

    [Fact]
    public void GetCombinedPolicy_ReturnsNonNullPolicy()
    {
        // Act
        var policy = _policies.GetCombinedPolicy();

        // Assert
        Assert.NotNull(policy);
    }

    [Fact]
    public async Task RetryPolicy_ShouldRetry_OnHttpRequestException()
    {
        // Arrange
        var policy = _policies.GetCombinedPolicy();
        var context = new Context("TestOperation");
        var attemptCount = 0;

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(async () =>
        {
            await policy.ExecuteAsync((ctx, ct) =>
            {
                attemptCount++;
                throw new HttpRequestException("Test exception");
                
                #pragma warning disable CS0162 // Unreachable code detected
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
                #pragma warning restore CS0162
            }, context, CancellationToken.None);
        });

        // Should have retried 3 times + initial attempt = 4 total attempts
        Assert.Equal(4, attemptCount);
    }

    [Fact]
    public async Task RetryPolicy_ShouldRetry_OnTimeoutException()
    {
        // Arrange
        var policy = _policies.GetCombinedPolicy();
        var context = new Context("TestOperation");
        var attemptCount = 0;

        // Act & Assert
        await Assert.ThrowsAsync<TimeoutException>(async () =>
        {
            await policy.ExecuteAsync((ctx, ct) =>
            {
                attemptCount++;
                throw new TimeoutException("Test timeout");
                
                #pragma warning disable CS0162 // Unreachable code detected
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
                #pragma warning restore CS0162
            }, context, CancellationToken.None);
        });

        // Should have retried 3 times + initial attempt = 4 total attempts
        Assert.Equal(4, attemptCount);
    }

    [Fact]
    public async Task RetryPolicy_ShouldRetry_On500StatusCode()
    {
        // Arrange
        var policy = _policies.GetCombinedPolicy();
        var context = new Context("TestOperation");
        var attemptCount = 0;

        // Act
        var response = await policy.ExecuteAsync(async (ctx, ct) =>
        {
            attemptCount++;
            
            // After 3 attempts, return a successful response to avoid exception
            if (attemptCount > 3)
            {
                return new HttpResponseMessage(HttpStatusCode.OK);
            }

            return new HttpResponseMessage(HttpStatusCode.InternalServerError);
        }, context, CancellationToken.None);

        // Assert
        Assert.Equal(4, attemptCount);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task RetryPolicy_ShouldRetry_OnTooManyRequests()
    {
        // Arrange
        var policy = _policies.GetCombinedPolicy();
        var context = new Context("TestOperation");
        var attemptCount = 0;

        // Act
        var response = await policy.ExecuteAsync(async (ctx, ct) =>
        {
            attemptCount++;
            
            // After 3 attempts, return a successful response to avoid exception
            if (attemptCount > 3)
            {
                return new HttpResponseMessage(HttpStatusCode.OK);
            }

            return new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        }, context, CancellationToken.None);

        // Assert
        Assert.Equal(4, attemptCount);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CircuitBreakerPolicy_ShouldBreakCircuit_OnConsistentFailures()
    {
        // Arrange
        // Create a new policy for testing to avoid affecting other tests
        var breakerPolicy = Policy
            .HandleResult<HttpResponseMessage>(r => r.StatusCode == HttpStatusCode.InternalServerError)
            .CircuitBreakerAsync(2, TimeSpan.FromMilliseconds(100));

        int failureCount = 0;
        int successCount = 0;
        bool circuitBroken = false;

        // Act - Generate failures to open circuit
        for (int i = 0; i < 5; i++)
        {
            try
            {
                await breakerPolicy.ExecuteAsync(async () => {
                    await Task.CompletedTask;
                    failureCount++;
                    return new HttpResponseMessage(HttpStatusCode.InternalServerError);
                });
            }
            catch (BrokenCircuitException)
            {
                // Expected after circuit breaks
                circuitBroken = true;
            }
        }

        // Assert
        Assert.True(circuitBroken, "Circuit should have been broken");
        Assert.True(failureCount >= 2, "Should have executed at least 2 calls before breaking");
        
        // Wait for circuit to reset to half-open state
        await Task.Delay(200);
        
        // Try a successful call to reset the circuit
        try
        {
            await breakerPolicy.ExecuteAsync(async () => {
                await Task.CompletedTask;
                successCount++;
                return new HttpResponseMessage(HttpStatusCode.OK);
            });
        }
        catch (Exception)
        {
            // If we happen to catch at just the wrong time, it might still be open
        }
        
        // Try another successful call which should work if circuit reset
        await breakerPolicy.ExecuteAsync(async () => {
            await Task.CompletedTask;
            successCount++;
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        
        Assert.True(successCount > 0, "Should execute at least one successful call after circuit resets");
    }
}