using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using CurrencyConverter.Core.Models;

namespace CurrencyConverter.API.Middleware;

public class ApiThrottlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ApiThrottlingMiddleware> _logger;
    private readonly int _requestLimit;
    private readonly TimeSpan _timeWindow;
    private readonly ConcurrentDictionary<string, Queue<DateTime>> _requestTimestamps;

    public ApiThrottlingMiddleware(
        RequestDelegate next,
        ILogger<ApiThrottlingMiddleware> logger,
        int requestLimit = 100,
        int timeWindowSeconds = 60)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _requestLimit = requestLimit;
        _timeWindow = TimeSpan.FromSeconds(timeWindowSeconds);
        _requestTimestamps = new ConcurrentDictionary<string, Queue<DateTime>>();
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Get client identifier (IP or ClientId from JWT)
        string clientId = GetClientIdentifier(context);

        if (IsRateLimitExceeded(clientId))
        {
            _logger.LogWarning("Rate limit exceeded for client {ClientId}", clientId);

            context.Response.StatusCode = (int)HttpStatusCode.TooManyRequests;
            context.Response.ContentType = "application/json";

            var response = ApiResponse<object>.ErrorResponse("Rate limit exceeded. Please try again later.");
            await context.Response.WriteAsync(JsonSerializer.Serialize(response));

            return;
        }

        await _next(context);
    }

    private string GetClientIdentifier(HttpContext context)
    {
        // Use ClientId from JWT if available, otherwise use IP
        string clientId = "anonymous";
        if (context.User.Identity?.IsAuthenticated == true)
        {
            clientId = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "authenticated";
        }
        else
        {
            // Fallback to IP address
            clientId = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        }

        return clientId;
    }

    private bool IsRateLimitExceeded(string clientId)
    {
        var now = DateTime.UtcNow;

        // Get or create queue for this client
        var timestamps = _requestTimestamps.GetOrAdd(clientId, _ => new Queue<DateTime>());

        // Remove timestamps outside the time window
        lock (timestamps)
        {
            while (timestamps.Count > 0 && now - timestamps.Peek() > _timeWindow)
            {
                timestamps.Dequeue();
            }

            // Check if the limit is reached
            if (timestamps.Count >= _requestLimit)
            {
                return true;
            }

            // Add current timestamp
            timestamps.Enqueue(now);
            return false;
        }
    }
}