using System.Diagnostics;
using System.Security.Claims;

namespace CurrencyConverter.API.Middleware;

public class RequestResponseLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestResponseLoggingMiddleware> _logger;

    public RequestResponseLoggingMiddleware(RequestDelegate next, ILogger<RequestResponseLoggingMiddleware> logger)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Start timing
        var stopwatch = Stopwatch.StartNew();

        // Capture client IP
        var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        // Try to get clientId from JWT token
        string clientId = "anonymous";
        if (context.User.Identity?.IsAuthenticated == true)
        {
            clientId = context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "authenticated";
        }

        // Capture request method and path
        var method = context.Request.Method;
        var path = context.Request.Path;

        // Create a correlation ID for request tracking
        var correlationId = Activity.Current?.Id ?? context.TraceIdentifier;

        // Add correlation ID to response headers (using proper header API)
        context.Response.Headers["X-Correlation-ID"] = correlationId;

        try
        {
            // Call the next middleware
            await _next(context);

            // Stop timing
            stopwatch.Stop();

            // Log the request after processing
            _logger.LogInformation(
                "Request {Method} {Path} processed in {ElapsedMilliseconds}ms with status code {StatusCode}. " +
                "Client: {ClientIp}, ClientId: {ClientId}, CorrelationId: {CorrelationId}",
                method, path, stopwatch.ElapsedMilliseconds, context.Response.StatusCode,
                clientIp, clientId, correlationId);
        }
        catch (Exception ex)
        {
            // Stop timing in case of exception
            stopwatch.Stop();

            // Log the exception
            _logger.LogError(
                ex,
                "Request {Method} {Path} failed after {ElapsedMilliseconds}ms. " +
                "Client: {ClientIp}, ClientId: {ClientId}, CorrelationId: {CorrelationId}",
                method, path, stopwatch.ElapsedMilliseconds,
                clientIp, clientId, correlationId);

            // Re-throw to let the error handling middleware deal with it
            throw;
        }
    }
}