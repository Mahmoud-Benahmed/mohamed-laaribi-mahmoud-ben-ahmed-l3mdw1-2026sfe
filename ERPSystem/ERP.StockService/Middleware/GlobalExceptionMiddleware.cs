using ERP.StockService.Application.DTOs;
using ERP.StockService.Application.Exceptions;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Text.Json;

namespace ERP.StockService.Middleware;

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
            _logger.LogError(ex, "Unhandled exception on {Method} {Path}",
                context.Request.Method, context.Request.Path);
            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        ErrorResponse response = exception switch
        {
            InsufficientStockException ex => new ErrorResponse
            {
                Code = "STOCK_001",
                Message = ex.Message,
                StatusCode = (int)HttpStatusCode.BadRequest
            },

            BonEntreNotFoundException ex => new ErrorResponse
            {
                Code = "STOCK_002",
                Message = ex.Message,
                StatusCode = (int)HttpStatusCode.NotFound
            },

            BonSortieNotFoundException ex => new ErrorResponse
            {
                Code = "STOCK_003",
                Message = ex.Message,
                StatusCode = (int)HttpStatusCode.NotFound
            },

            BonRetourNotFoundException ex => new ErrorResponse
            {
                Code = "STOCK_004",
                Message = ex.Message,
                StatusCode = (int)HttpStatusCode.NotFound
            },

            ArticleNotFoundException ex => new ErrorResponse
            {
                Code = "STOCK_006",
                Message = ex.Message,
                StatusCode = (int)HttpStatusCode.NotFound
            },

            ClientNotFoundException ex => new ErrorResponse
            {
                Code = "STOCK_007",
                Message = ex.Message,
                StatusCode = (int)HttpStatusCode.NotFound
            },

            // ── Retour business rules ─────────────────────────────────────────
            ArticleNotInSourceBonException ex => new ErrorResponse
            {
                Code = "STOCK_008",
                Message = ex.Message,
                StatusCode = (int)HttpStatusCode.UnprocessableEntity
            },

            RetourQuantityExceedsSourceException ex => new ErrorResponse
            {
                Code = "STOCK_009",
                Message = ex.Message,
                StatusCode = (int)HttpStatusCode.UnprocessableEntity
            },
            FournisseurNotFoundException ex => new ErrorResponse
            {
                Code = "STOCK_010",
                Message = ex.Message,
                StatusCode = (int)HttpStatusCode.NotFound
            },
            FournisseurBlockedException ex => new ErrorResponse
            {
                Code = "STOCK_011",
                Message = ex.Message,
                StatusCode = (int)HttpStatusCode.Forbidden
            },
            ClientBlockedException ex => new ErrorResponse
            {
                Code = "STOCK_012",
                Message = ex.Message,
                StatusCode = (int)HttpStatusCode.Forbidden
            },

            // ── External service unavailable ─────────────────────────────────
            HttpRequestException ex => new ErrorResponse
            {
                Code = "EXTERNAL_SERVICE_ERROR",
                Message = "An external service is currently unavailable. Please try again later.",
                StatusCode = (int)HttpStatusCode.ServiceUnavailable
            },

            // ── Generic ───────────────────────────────────────────────────────
            KeyNotFoundException ex => new ErrorResponse
            {
                Code = "NOT_FOUND",
                Message = ex.Message,
                StatusCode = (int)HttpStatusCode.NotFound
            },
            ArgumentOutOfRangeException ex => new ErrorResponse
            {
                Code = "INVALID_RANGE",
                Message = ex.Message,
                StatusCode = (int)HttpStatusCode.BadRequest
            },

            ArgumentException ex => new ErrorResponse
            {
                Code = "VALIDATION_ERROR",
                Message = ex.Message,
                StatusCode = (int)HttpStatusCode.BadRequest
            },

            // ── Database ──────────────────────────────────────────────────────
            DbUpdateException ex when IsDuplicateKeyException(ex) => new ErrorResponse
            {
                Code = "DUPLICATE_ENTRY",
                Message = ExtractDuplicateField(ex.InnerException!.Message),
                StatusCode = (int)HttpStatusCode.Conflict
            },

            DbUpdateException => new ErrorResponse
            {
                Code = "DATABASE_ERROR",
                Message = "A database error occurred.",
                StatusCode = (int)HttpStatusCode.InternalServerError
            },

            _ => new ErrorResponse
            {
                Code = "INTERNAL_SERVER_ERROR",
                Message = "An unexpected error occurred.",
                StatusCode = (int)HttpStatusCode.InternalServerError
            }
        };

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = response.StatusCode;

        await context.Response.WriteAsync(
            JsonSerializer.Serialize(response,
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
    }

    private static bool IsDuplicateKeyException(DbUpdateException ex) =>
        ex.InnerException?.Message.Contains("unique index", StringComparison.OrdinalIgnoreCase) == true ||
        ex.InnerException?.Message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase) == true;

    private static string ExtractDuplicateField(string message)
    {
        if (message.Contains("IX_Fournisseurs_TaxNumber", StringComparison.OrdinalIgnoreCase))
            return "A supplier with this tax number already exists.";

        if (message.Contains("IX_BonEntres_Numero", StringComparison.OrdinalIgnoreCase))
            return "A BonEntre with this numero already exists.";

        if (message.Contains("IX_BonSorties_Numero", StringComparison.OrdinalIgnoreCase))
            return "A BonSortie with this numero already exists.";

        if (message.Contains("IX_BonRetours_Numero", StringComparison.OrdinalIgnoreCase))
            return "A BonRetour with this numero already exists.";

        return "A record with this value already exists.";
    }
}