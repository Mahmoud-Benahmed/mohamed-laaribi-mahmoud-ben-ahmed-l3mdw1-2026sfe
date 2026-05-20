using ERP.TenantService.Application.DTOs;
using ERP.TenantService.Application.Exceptions;
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

        ErrorResponse response = exception switch
        {
            TenantNotFoundException ex => new ErrorResponse
            {
                Code = "TENANT_001",
                Message = ex.Message,
                StatusCode = (int)HttpStatusCode.NotFound
            },
            TenantSubscriptionNotFoundException ex => new ErrorResponse
            {
                Code = "TENANT_002",
                Message = ex.Message,
                StatusCode = (int)HttpStatusCode.NotFound
            },
            SubdomainAlreadyTakenException ex => new ErrorResponse
            {
                Code = "TENANT_003",
                Message = ex.Message,
                StatusCode = (int)HttpStatusCode.Conflict
            },
            KeyNotFoundException => new ErrorResponse{ StatusCode= (int) HttpStatusCode.NotFound, Code = "NOT_FOUND", Message= exception.Message},
            InvalidOperationException => new ErrorResponse{ StatusCode = (int)HttpStatusCode.BadRequest,Code = "VALIDATION_ERROR",Message = exception.Message},
            UnauthorizedAccessException => new ErrorResponse { StatusCode = (int)HttpStatusCode.Unauthorized, Code = "UNAUTHORIZED", Message = exception.Message },
            ArgumentException => new ErrorResponse { StatusCode = (int)HttpStatusCode.BadRequest, Code = "INVALID_ARGUMENT", Message = exception.Message },
            _ => new ErrorResponse { StatusCode = (int)HttpStatusCode.InternalServerError, Code = "INTERNAL_SERVER_ERROR", Message = "An unexpected error occurred." }
        };

        context.Response.StatusCode = response.StatusCode;

        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await context.Response.WriteAsync(json);
    }
}
