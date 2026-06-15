// Middleware/GlobalExceptionMiddleware.cs
using ERP.PaymentService.Application.Exceptions;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Text.Json;

namespace ERP.PaymentService.Middleware;

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
            PaymentNotFoundException e => new ErrorResponse
            {
                Code = "PAYMENT_NOT_FOUND",
                Message = e.Message,
                StatusCode = (int)HttpStatusCode.NotFound
            },

            InvoiceNotFoundException e => new ErrorResponse
            {
                Code = "INVOICE_NOT_FOUND",
                Message = e.Message,
                StatusCode = (int)HttpStatusCode.NotFound
            },

            KeyNotFoundException e => new ErrorResponse
            {
                Code = "NOT_FOUND",
                Message = e.Message,
                StatusCode = (int)HttpStatusCode.NotFound
            },

            PaymentAlreadyCancelledException e => new ErrorResponse
            {
                Code = "PAYMENT_ALREADY_CANCELLED",
                Message = e.Message,
                StatusCode = (int)HttpStatusCode.Conflict
            },

            InvoiceAlreadyPaidException e => new ErrorResponse
            {
                Code = "INVOICE_ALREADY_PAID",
                Message = e.Message,
                StatusCode = (int)HttpStatusCode.Conflict
            },

            InvoiceAlreadyCancelledException e => new ErrorResponse
            {
                Code = "INVOICE_ALREADY_CANCELLED",
                Message = e.Message,
                StatusCode = (int)HttpStatusCode.Conflict
            },

            PaymentDomainException e => new ErrorResponse
            {
                Code = "PAYMENT_DOMAIN_ERROR",
                Message = e.Message,
                StatusCode = (int)HttpStatusCode.BadRequest
            },

            // Handle duplicate key / unique constraint violations
            DbUpdateException e when
                e.InnerException?.Message.Contains("unique index") == true ||
                e.InnerException?.Message.Contains("duplicate key") == true =>
                new ErrorResponse
                {
                    Code = ExtractDuplicateCode(e.InnerException!.Message),
                    Message = ExtractDuplicateMessage(e.InnerException.Message),
                    StatusCode = (int)HttpStatusCode.Conflict
                },

            DbUpdateException e => new ErrorResponse
            {
                Code = "DATABASE_ERROR",
                Message = "A database error occurred. Please try again later.",
                StatusCode = (int)HttpStatusCode.InternalServerError
            },

            ArgumentException e => new ErrorResponse
            {
                Code = "INVALID_ARGUMENT",
                Message = e.Message,
                StatusCode = (int)HttpStatusCode.BadRequest
            },

            InvalidOperationException e => new ErrorResponse
            {
                Code = "INVALID_OPERATION",
                Message = e.Message,
                StatusCode = (int)HttpStatusCode.BadRequest
            },

            UnauthorizedAccessException e => new ErrorResponse
            {
                Code = "UNAUTHORIZED",
                Message = e.Message,
                StatusCode = (int)HttpStatusCode.Unauthorized
            },

            _ => new ErrorResponse
            {
                Code = "INTERNAL_SERVER_ERROR",
                Message = "An internal error occurred. Please try again later.",
                StatusCode = (int)HttpStatusCode.InternalServerError
            }
        };

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = response.StatusCode;

        await context.Response.WriteAsync(JsonSerializer.Serialize(response,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
    }

    private static string ExtractDuplicateCode(string innerMessage)
    {
        // Adjust index names according to your PaymentService database schema
        if (innerMessage.Contains("IX_Payments_Number"))
            return "DUPLICATE_PAYMENT_NUMBER";
        if (innerMessage.Contains("IX_Payments_ExternalReference"))
            return "DUPLICATE_EXTERNAL_REFERENCE";
        return "DUPLICATE_ENTRY";
    }

    private static string ExtractDuplicateMessage(string innerMessage)
    {
        if (innerMessage.Contains("IX_Payments_Number"))
            return "A payment with this number already exists.";
        if (innerMessage.Contains("IX_Payments_ExternalReference"))
            return "A payment with this external reference already exists.";
        return "A duplicate entry was detected.";
    }
}

internal class ErrorResponse
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public int StatusCode { get; set; }
}