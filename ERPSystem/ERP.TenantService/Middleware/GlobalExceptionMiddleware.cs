using System.Net;
using System.Text.Json;

namespace ERP.TenantService.Middleware;

public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception: {Message}", ex.Message);
            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";

        var (statusCode, code, message) = exception switch
        {
            KeyNotFoundException => (HttpStatusCode.NotFound, "NOT_FOUND", exception.Message),
            InvalidOperationException => (HttpStatusCode.BadRequest, "VALIDATION_ERROR", exception.Message),
            UnauthorizedAccessException => (HttpStatusCode.Unauthorized, "UNAUTHORIZED", exception.Message),
            _ => (HttpStatusCode.InternalServerError, "INTERNAL_SERVER_ERROR", "An unexpected error occurred.")
        };

        context.Response.StatusCode = (int)statusCode;

        var response = new
        {
            statusCode = (int)statusCode,
            code,
            message
        };

        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await context.Response.WriteAsync(json);
    }
}
