using ERP.FournisseurService.Application.Exceptions;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Text.Json;

namespace ERP.FournisseurService.Middleware;

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
            _logger.LogError(ex, "An unhandled exception occurred.");
            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        ErrorResponse response = exception switch
        {
            FournisseurNotFoundException ex => new ErrorResponse
            {
                Code = "FOURNISSEUR_NOT_FOUND",
                Message = ex.Message,
                StatusCode = (int)HttpStatusCode.NotFound
            },

            FournisseurBlockedException ex => new ErrorResponse
            {
                Code = "FOURNISSEUR_BLOCKED",   // Add this key to frontend
                Message = ex.Message,
                StatusCode = (int)HttpStatusCode.Conflict
            },

            KeyNotFoundException ex => new ErrorResponse
            {
                Code = "NOT_FOUND",
                Message = ex.Message,
                StatusCode = (int)HttpStatusCode.NotFound
            },

            ArgumentOutOfRangeException ex => new ErrorResponse
            {
                Code = "INVALID_RANGE",         // ← changed
                Message = ex.Message,
                StatusCode = (int)HttpStatusCode.BadRequest
            },

            ArgumentNullException ex => new ErrorResponse
            {
                Code = "VALIDATION_ERROR",      // ← changed
                Message = ex.Message,
                StatusCode = (int)HttpStatusCode.BadRequest
            },

            ArgumentException ex => new ErrorResponse
            {
                Code = "BAD_ARGUMENT",          // keep or change to VALIDATION_ERROR
                Message = ex.Message,
                StatusCode = (int)HttpStatusCode.BadRequest
            },

            InvalidOperationException ex => new ErrorResponse
            {
                Code = "BUSINESS_RULE_VIOLATION", // ← changed
                Message = ex.Message,
                StatusCode = (int)HttpStatusCode.Conflict
            },

            DuplicateKeyException ex => new ErrorResponse
            {
                Code = "DUPLICATE_ENTRY",
                Message = ex.Message,
                StatusCode = (int)HttpStatusCode.Conflict
            },

            DbUpdateException ex when
                ex.InnerException?.Message.Contains("unique index") == true ||
                ex.InnerException?.Message.Contains("duplicate key") == true =>
                new ErrorResponse
                {
                    Code = "DUPLICATE_ENTRY",    // ← use generic key
                    Message = ExtractDuplicateMessage(ex.InnerException!.Message),
                    StatusCode = (int)HttpStatusCode.Conflict
                },

            DbUpdateException ex => new ErrorResponse
            {
                Code = "DATABASE_ERROR",
                Message = "A database error occurred. Please try again later.",
                StatusCode = (int)HttpStatusCode.InternalServerError
            },

            UnauthorizedAccessException ex => new ErrorResponse
            {
                Code = "UNAUTHORIZED",
                Message = ex.Message,
                StatusCode = (int)HttpStatusCode.Unauthorized
            },

            _ => new ErrorResponse
            {
                Code = "INTERNAL_SERVER_ERROR",  // ← changed
                Message = "An internal error occurred. Please try again later.",
                StatusCode = (int)HttpStatusCode.InternalServerError
            }
        };

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = response.StatusCode;

        await context.Response.WriteAsync(JsonSerializer.Serialize(response,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
    }

    private static string ExtractDuplicateMessage(string innerMessage)
    {
        if (innerMessage.Contains("IX_Suppliers_Name"))
            return "A supplier with this name already exists.";
        if (innerMessage.Contains("IX_Suppliers_Email"))
            return "A supplier with this email already exists.";
        if (innerMessage.Contains("IX_Suppliers_TaxNumber"))
            return "A supplier with this tax number already exists.";
        if (innerMessage.Contains("IX_Suppliers_Phone"))
            return "A supplier with this phone number already exists.";
        return "A duplicate entry was detected.";
    }
}

// Reusable error response DTO (you can move it to a shared project if needed)
internal class ErrorResponse
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public int StatusCode { get; set; }
}