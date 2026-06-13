using InvoiceService.Application.Exceptions;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Text.Json;

namespace InvoiceService.Middleware;

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
            KeyNotFoundException e => new ErrorResponse
            {
                Code = "NOT_FOUND",
                Message = e.Message,
                StatusCode = (int)HttpStatusCode.NotFound
            },

            InvoiceNotFoundException e => new ErrorResponse
            {
                Code = "INVOICE_NOT_FOUND",
                Message = e.Message,
                StatusCode = (int)HttpStatusCode.NotFound
            },

            InvoiceAlreadyExistsException e => new ErrorResponse
            {
                Code = "INVOICE_ALREADY_EXISTS",
                Message = e.Message,
                StatusCode = (int)HttpStatusCode.Conflict
            },

            InvoiceInvalidOperationException e => new ErrorResponse
            {
                Code = "INVOICE_INVALID_OPERATION",
                Message = e.Message,
                StatusCode = (int)HttpStatusCode.BadRequest
            },

            InvoiceDomainException e => new ErrorResponse
            {
                Code = "INVOICE_DOMAIN_ERROR",
                Message = e.Message,
                StatusCode = (int)HttpStatusCode.BadRequest
            },

            ClientBlockedException e => new ErrorResponse
            {
                Code = "CLIENT_BLOCKED",
                Message = e.Message,
                StatusCode = (int)HttpStatusCode.Forbidden
            },

            // Handle duplicate invoice number → reuse existing key
            DbUpdateException e when
                e.InnerException?.Message.Contains("IX_Invoices_TenantId_InvoiceNumber") == true =>
                new ErrorResponse
                {
                    Code = "INVOICE_ALREADY_EXISTS",   // ← changed
                    Message = "An invoice with this number already exists for this tenant.",
                    StatusCode = (int)HttpStatusCode.Conflict
                },

            // Other DB errors → use existing DATABASE_ERROR (will fallback to stock namespace)
            DbUpdateException e => new ErrorResponse
            {
                Code = "DATABASE_ERROR",
                Message = "A database error occurred. Please try again later.",
                StatusCode = (int)HttpStatusCode.InternalServerError
            },

            // Argument exceptions → map to VALIDATION_ERROR (exists in tenants namespace)
            ArgumentOutOfRangeException e => new ErrorResponse
            {
                Code = "VALIDATION_ERROR",
                Message = e.Message,
                StatusCode = (int)HttpStatusCode.BadRequest
            },

            ArgumentNullException e => new ErrorResponse
            {
                Code = "VALIDATION_ERROR",
                Message = e.Message,
                StatusCode = (int)HttpStatusCode.BadRequest
            },

            ArgumentException e => new ErrorResponse
            {
                Code = "VALIDATION_ERROR",
                Message = e.Message,
                StatusCode = (int)HttpStatusCode.BadRequest
            },

            UnauthorizedAccessException e => new ErrorResponse
            {
                Code = "UNAUTHORIZED",   // exists in auth namespace
                Message = e.Message,
                StatusCode = (int)HttpStatusCode.Unauthorized
            },

            _ => new ErrorResponse
            {
                Code = "INTERNAL_SERVER_ERROR",   // ← changed to match frontend key
                Message = "An internal error occurred. Please try again later.",
                StatusCode = (int)HttpStatusCode.InternalServerError
            }
        };

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = response.StatusCode;

        await context.Response.WriteAsync(JsonSerializer.Serialize(response,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
    }
}

internal class ErrorResponse
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public int StatusCode { get; set; }
}