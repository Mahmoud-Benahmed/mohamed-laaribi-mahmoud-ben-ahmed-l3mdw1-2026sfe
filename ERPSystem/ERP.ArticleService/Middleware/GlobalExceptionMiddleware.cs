using ERP.ArticleService.Application.DTOs;
using ERP.ArticleService.Application.Exceptions;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Text.Json;

namespace ERP.ArticleService.Middleware
{
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
                ArticleNotFoundException ex => new ErrorResponse
                {
                    Code = "ART_001",
                    Message = "ART_001",
                    StatusCode = (int)HttpStatusCode.NotFound
                },

                ArticleAlreadyExistsException ex => new ErrorResponse
                {
                    Code = "ART_002",
                    Message = "ART_002",
                    StatusCode = (int)HttpStatusCode.BadRequest
                },

                ArticleAlreadyActiveException ex => new ErrorResponse
                {
                    Code = "ART_003",
                    Message = "ART_003",
                    StatusCode = (int)HttpStatusCode.BadRequest
                },

                ArticleAlreadyInactiveException ex => new ErrorResponse
                {
                    Code = "ART_004",
                    Message = "ART_004",
                    StatusCode = (int)HttpStatusCode.BadRequest
                },

                CategoryNotFoundException ex => new ErrorResponse
                {
                    Code = "CAT_001",
                    Message = "CAT_001",
                    StatusCode = (int)HttpStatusCode.NotFound
                },

                CategoryAlreadyExistsException ex => new ErrorResponse
                {
                    Code = "CAT_002",
                    Message = "CAT_002",
                    StatusCode = (int)HttpStatusCode.BadRequest
                },

                CategoryAssignedToArticlesException ex => new ErrorResponse
                {
                    Code = "ARTICLE_CATEGORY_DELETE_FAIL",
                    Message = "ARTICLE_CATEGORY_DELETE_FAIL",
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
                    ex.InnerException?.Message.Contains("duplicate key") == true => new ErrorResponse
                    {
                        Code = "DUPLICATE_ENTRY",
                        Message = ExtractDuplicateField(ex.InnerException!.Message),
                        StatusCode = (int)HttpStatusCode.Conflict
                    },

                DbUpdateException ex => new ErrorResponse
                {
                    Code = "DATABASE_ERROR",
                    Message = "DATABASE_ERROR",
                    StatusCode = (int)HttpStatusCode.InternalServerError
                },

                // ── Generic
                KeyNotFoundException ex => new ErrorResponse
                {
                    Code = "NOT_FOUND",
                    Message = "NOT_FOUND",
                    StatusCode = (int)HttpStatusCode.NotFound
                },

                ArgumentOutOfRangeException ex => new ErrorResponse
                {
                    Code = "OUT_OF_RANGE",
                    Message = "OUT_OF_RANGE",
                    StatusCode = (int)HttpStatusCode.BadRequest
                },

                ArgumentNullException ex => new ErrorResponse
                {
                    Code = "NULL_ARGUMENT",
                    Message = "NULL_ARGUMENT",
                    StatusCode = (int)HttpStatusCode.BadRequest
                },

                ArgumentException ex => new ErrorResponse
                {
                    Code = "BAD_ARGUMENT",
                    Message = "BAD_ARGUMENT",
                    StatusCode = (int)HttpStatusCode.BadRequest
                },

                InvalidOperationException ex => new ErrorResponse
                {
                    Code = "INVALID_OP",
                    Message = "INVALID_OP",
                    StatusCode = (int)HttpStatusCode.BadRequest
                },

                UnauthorizedAccessException ex => new ErrorResponse
                {
                    Code = "UNAUTHORIZED",
                    Message = "UNAUTHORIZED",
                    StatusCode = (int)HttpStatusCode.Unauthorized
                },

                _ => new ErrorResponse
                {
                    Code = "SERVER_ERROR",
                    Message = "SERVER_ERROR",
                    StatusCode = (int)HttpStatusCode.InternalServerError
                }
            };

            context.Response.ContentType = "application/json";
            context.Response.StatusCode = response.StatusCode;

            await context.Response.WriteAsync(JsonSerializer.Serialize(response,
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
        }

        private static string ExtractDuplicateField(string message)
        {
            if (message.Contains("IX_Articles_CodeRef"))
                return "DUPLICATE_ARTICLE_CODE";

            if (message.Contains("IX_Articles_BarCode"))
                return "DUPLICATE_ARTICLE_BARCODE";

            if (message.Contains("IX_Categories_Name"))
                return "DUPLICATE_CATEGORY_NAME";

            return "DUPLICATE_ENTRY";
        }
    }
}