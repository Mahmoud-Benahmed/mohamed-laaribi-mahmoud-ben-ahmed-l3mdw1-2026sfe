using ERP.AuthService.Application.Exceptions;
using ERP.AuthService.Application.Interfaces.Services;
using ERP.AuthService.Domain.Logger;
using MongoDB.Driver;
using System.Net;
using System.Security;

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

    public async Task InvokeAsync(HttpContext context, IAuditLogger auditLogger)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Unhandled exception | {Method} {Path} | IP: {IP}",
                context.Request.Method,
                context.Request.Path,
                context.Connection.RemoteIpAddress);

            await LogAuditFailureAsync(context, ex, auditLogger);
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task LogAuditFailureAsync(
        HttpContext context,
        Exception exception,
        IAuditLogger auditLogger)
    {
        try
        {

            string? sub = context.Request.Headers["X-User-Id"].FirstOrDefault();

            Guid.TryParse(sub, out Guid performedBy);

            AuditAction action = exception switch
            {
                InvalidCredentialsException => AuditAction.Login,
                InvalidRefreshTokenException => AuditAction.TokenRefreshed,
                TokenAlreadyRevokedException => AuditAction.TokenRevoked,

                UserInactiveException => AuditAction.Login,
                LoginAlreadyExsistException => AuditAction.UserRegistered,
                EmailAlreadyExistsException => AuditAction.UserRegistered,

                UnauthorizedOperationException => AuditAction.Unauthorized,
                UserNotFoundException => AuditAction.UserNotFound,

                _ => AuditAction.UnhandledError
            };

            await auditLogger.LogAsync(
                action,
                success: false,
                performedBy: performedBy,
                failureReason: exception.Message,
                ipAddress: context.Connection.RemoteIpAddress?.ToString(),
                metadata: new()
                {
                    ["exceptionType"] = exception.GetType().Name,
                    ["path"] = context.Request.Path,
                    ["method"] = context.Request.Method
                });
        }
        catch
        {
            // ✅ closing brace was missing — never let audit failure break the response
        }
    }

    private static Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        (int statusCode, string? code, string? message) = exception switch
        {
            EmailAlreadyExistsException => ((int)HttpStatusCode.Conflict, "AUTH_001", "AUTH_001"),
            InvalidCredentialsException => ((int)HttpStatusCode.Unauthorized, "AUTH_002", "AUTH_002"),
            UserInactiveException => ((int)HttpStatusCode.Forbidden, "AUTH_003", "AUTH_003"),
            UserActiveException => ((int)HttpStatusCode.Forbidden, "AUTH_004", "AUTH_004"),
            TokenAlreadyRevokedException => ((int)HttpStatusCode.BadRequest, "AUTH_005", "AUTH_005"),
            UnauthorizedAccessException => ((int)HttpStatusCode.Unauthorized, "AUTH_006", "AUTH_006"),
            UnauthorizedOperationException => ((int)HttpStatusCode.Forbidden, "AUTH_007", "AUTH_007"),
            SecurityException => ((int)HttpStatusCode.Unauthorized, "AUTH_008", "AUTH_008"),
            UserNotFoundException => ((int)HttpStatusCode.NotFound, "AUTH_009", "AUTH_009"),
            RoleNotFoundException => ((int)HttpStatusCode.NotFound, "AUTH_010", "AUTH_010"),
            ControleNotFoundException => ((int)HttpStatusCode.NotFound, "AUTH_011", "AUTH_011"),
            PrivilegeNotFoundException => ((int)HttpStatusCode.NotFound, "AUTH_012", "AUTH_012"),
            ArgumentException => ((int)HttpStatusCode.BadRequest, "AUTH_013", "AUTH_013"),
            InvalidOperationException => ((int)HttpStatusCode.BadRequest, "AUTH_014", "AUTH_014"),
            LoginAlreadyExsistException => ((int)HttpStatusCode.Conflict, "AUTH_015", "AUTH_015"),
            DuplicateKeyException ex => ((int)HttpStatusCode.Conflict, "DUPLICATE_ENTRY",ex.Message),
            FluentValidation.ValidationException vex => ((int)HttpStatusCode.BadRequest, "AUTH_016", string.Join(", ", vex.Errors.Select(e => e.ErrorMessage))),
            MongoWriteException mwx when mwx.WriteError?.Code == 11000 =>
                                                                            ((int)HttpStatusCode.Conflict, "AUTH_017", "AUTH_017"),
            InvalidRefreshTokenException => ((int)HttpStatusCode.Unauthorized, "AUTH_018", "AUTH_018"),
            TenantUserLimitReachedException => ((int)HttpStatusCode.Forbidden, "TENANT_USER_LIMIT_REACHED", "TENANT_USER_LIMIT_REACHED"),
            _ => ((int)HttpStatusCode.InternalServerError, "INTERNAL_ERROR", "AUTH_000")
        };

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = statusCode;
        return context.Response.WriteAsJsonAsync(new { statusCode, code, message });
    }
}