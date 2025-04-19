using System.Net;
using System.Text.Json;
using CurrencyConverter.Core.Models;

namespace CurrencyConverter.API.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var statusCode = HttpStatusCode.InternalServerError;
        var message = "An unexpected error occurred.";

        // Determine the appropriate status code and message based on exception type
        switch (exception)
        {
            case ArgumentException argEx:
                statusCode = HttpStatusCode.BadRequest;
                message = argEx.Message;
                break;
            case InvalidOperationException invOpEx:
                statusCode = HttpStatusCode.BadRequest;
                message = invOpEx.Message;
                break;
            case UnauthorizedAccessException unauthEx:
                statusCode = HttpStatusCode.Unauthorized;
                message = "Unauthorized access.";
                break;
            case HttpRequestException httpEx:
                statusCode = HttpStatusCode.ServiceUnavailable;
                message = "External service unavailable.";
                break;
            default:
                // For unexpected exceptions, we keep the generic message but log the details
                _logger.LogError(exception, "Unhandled exception");
                break;
        }

        var response = ApiResponse<object>.ErrorResponse(message);

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;

        var jsonResponse = JsonSerializer.Serialize(response);
        await context.Response.WriteAsync(jsonResponse);
    }
}