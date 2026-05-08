using ERP.ClientService.Application.DTOs;
using ERP.ClientService.Application.Exceptions;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Text.Json;

namespace ERP.ClientService.Middleware;

public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionMiddleware> logger)
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
            _logger.LogError(ex, "Unhandled exception on {Method} {Path}",
                context.Request.Method, context.Request.Path);

            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(
        HttpContext context, Exception exception)
    {
        ErrorResponse response = exception switch
        {
            InvalidTenantException => new ErrorResponse
            {
                Code = "INVALID_TENANT",
                Message = "INVALID_TENANT",
                StatusCode = (int)HttpStatusCode.BadRequest
            },

            ClientNotFoundException => new ErrorResponse
            {
                Code = "CLIENT_NOT_FOUND",
                Message = "CLIENT_NOT_FOUND",
                StatusCode = (int)HttpStatusCode.NotFound
            },

            CategoryAssignedToUsersException => new ErrorResponse
            {
                Code = "CLIENT_CATEGORY_DELETE_FAIL",
                Message = "CLIENT_CATEGORY_DELETE_FAIL",
                StatusCode = (int)HttpStatusCode.Conflict
            },

            CategoryNotFoundException => new ErrorResponse
            {
                Code = "CATEGORY_NOT_FOUND",
                Message = "CATEGORY_NOT_FOUND",
                StatusCode = (int)HttpStatusCode.NotFound
            },

            KeyNotFoundException => new ErrorResponse
            {
                Code = "NOT_FOUND",
                Message = "NOT_FOUND",
                StatusCode = (int)HttpStatusCode.NotFound
            },

            InvalidOperationException => new ErrorResponse
            {
                Code = "BUSINESS_RULE_VIOLATION",
                Message = "BUSINESS_RULE_VIOLATION",
                StatusCode = (int)HttpStatusCode.Conflict
            },

            ArgumentOutOfRangeException => new ErrorResponse
            {
                Code = "INVALID_RANGE",
                Message = "INVALID_RANGE",
                StatusCode = (int)HttpStatusCode.BadRequest
            },

            ArgumentException => new ErrorResponse
            {
                Code = "VALIDATION_ERROR",
                Message = "VALIDATION_ERROR",
                StatusCode = (int)HttpStatusCode.BadRequest
            },

            DbUpdateException ex when IsDuplicateKeyException(ex) => new ErrorResponse
            {
                Code = "DUPLICATE_ENTRY",
                Message = ExtractDuplicateField(ex.InnerException!.Message),
                StatusCode = (int)HttpStatusCode.Conflict
            },

            DbUpdateException => new ErrorResponse
            {
                Code = "DATABASE_ERROR",
                Message = "DATABASE_ERROR",
                StatusCode = (int)HttpStatusCode.InternalServerError
            },

            _ => new ErrorResponse
            {
                Code = "INTERNAL_SERVER_ERROR",
                Message = "INTERNAL_SERVER_ERROR",
                StatusCode = (int)HttpStatusCode.InternalServerError
            }
        };

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = response.StatusCode;

        await context.Response.WriteAsync(
            JsonSerializer.Serialize(response,
                new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                }));
    }

    private static bool IsDuplicateKeyException(DbUpdateException ex) =>
        ex.InnerException?.Message.Contains("unique index", StringComparison.OrdinalIgnoreCase) == true ||
        ex.InnerException?.Message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase) == true;

    private static string ExtractDuplicateField(string message)
    {
        if (message.Contains("IX_Clients_Email", StringComparison.OrdinalIgnoreCase))
            return "DUPLICATE_CLIENT_EMAIL";

        if (message.Contains("IX_Clients_Name", StringComparison.OrdinalIgnoreCase))
            return "DUPLICATE_CLIENT_NAME";

        if (message.Contains("IX_Categories_Code", StringComparison.OrdinalIgnoreCase))
            return "DUPLICATE_CATEGORY_CODE";

        return "DUPLICATE_ENTRY";
    }
}