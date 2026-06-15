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
            // ── 404 Not Found ──────────────────────────────────────────────
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
            SubscriptionPlanNotFoundException ex => new ErrorResponse
            {
                Code = "PLAN_001",
                Message = ex.Message,
                StatusCode = (int)HttpStatusCode.NotFound
            },
            KeyNotFoundException => new ErrorResponse
            {
                StatusCode = (int)HttpStatusCode.NotFound,
                Code = "NOT_FOUND",
                Message = exception.Message
            },

            // ── 409 Conflict ───────────────────────────────────────────────
            DuplicateKeyException ex => new ErrorResponse
            {
                Code = "DUPLICATE_ENTRY",
                Message = ex.Message,
                StatusCode = (int)HttpStatusCode.Conflict
            },
            TenantAlreadyExistsException ex => new ErrorResponse
            {
                Code = "TENANT_004",
                Message = ex.Message,
                StatusCode = (int)HttpStatusCode.Conflict
            },
            TenantHasActiveSubscriptionException ex => new ErrorResponse
            {
                Code = "TENANT_005",
                Message = ex.Message,
                StatusCode = (int)HttpStatusCode.Conflict
            },
            SubscriptionPlanAlreadyExistsException ex => new ErrorResponse
            {
                Code = "PLAN_002",
                Message = ex.Message,
                StatusCode = (int)HttpStatusCode.Conflict
            },
            SubscriptionPlanInUseException ex => new ErrorResponse
            {
                Code = "PLAN_003",
                Message = ex.Message,
                StatusCode = (int)HttpStatusCode.Conflict
            },
            SubscriptionAlreadyExistsException ex => new ErrorResponse
            {
                Code = "SUBSCRIPTION_001",
                Message = ex.Message,
                StatusCode = (int)HttpStatusCode.Conflict
            },

            // ── 400 Bad Request ────────────────────────────────────────────
            TenantAlreadyActiveException ex => new ErrorResponse
            {
                Code = "TENANT_006",
                Message = ex.Message,
                StatusCode = (int)HttpStatusCode.BadRequest
            },
            TenantAlreadyDeletedException ex => new ErrorResponse
            {
                Code = "TENANT_007",
                Message = ex.Message,
                StatusCode = (int)HttpStatusCode.BadRequest
            },
            UnableDeleteTenantHasActiveSubscriptionException ex => new ErrorResponse
            {
                Code = "TENANT_008",
                Message = ex.Message,
                StatusCode = (int)HttpStatusCode.BadRequest
            },
            SubscriptionPlanInactiveException ex => new ErrorResponse
            {
                Code = "PLAN_004",
                Message = ex.Message,
                StatusCode = (int)HttpStatusCode.BadRequest
            },
            SubscriptionAssignmentFailedException ex => new ErrorResponse
            {
                Code = "SUBSCRIPTION_002",
                Message = ex.Message,
                StatusCode = (int)HttpStatusCode.BadRequest
            },
            InvalidOperationException => new ErrorResponse
            {
                StatusCode = (int)HttpStatusCode.BadRequest,
                Code = "VALIDATION_ERROR",
                Message = exception.Message
            },
            ArgumentException => new ErrorResponse
            {
                StatusCode = (int)HttpStatusCode.BadRequest,
                Code = "INVALID_ARGUMENT",
                Message = exception.Message
            },

            // ── 401 Unauthorized ───────────────────────────────────────────
            UnauthorizedAccessException => new ErrorResponse
            {
                StatusCode = (int)HttpStatusCode.Unauthorized,
                Code = "UNAUTHORIZED",
                Message = exception.Message
            },

            // ── 500 Internal Server Error (fallback) ───────────────────────
            _ => new ErrorResponse
            {
                StatusCode = (int)HttpStatusCode.InternalServerError,
                Code = "INTERNAL_SERVER_ERROR",
                Message = "An unexpected error occurred."
            }
        };

        context.Response.StatusCode = response.StatusCode;

        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await context.Response.WriteAsync(json);
    }
}