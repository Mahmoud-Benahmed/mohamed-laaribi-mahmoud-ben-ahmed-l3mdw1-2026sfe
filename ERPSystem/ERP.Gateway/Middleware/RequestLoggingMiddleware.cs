namespace ERP.Gateway.Middleware;

public sealed class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(
        RequestDelegate next,
        ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        DateTime start = DateTime.UtcNow;

        await _next(context);

        string userId =
            context.User.FindFirst("sub")?.Value
            ?? "anonymous";

        double duration =
            (DateTime.UtcNow - start).TotalMilliseconds;

        _logger.LogInformation(
            "Request {Method} {Path} => {StatusCode} in {Duration}ms User:{UserId}",
            context.Request.Method,
            context.Request.Path,
            context.Response.StatusCode,
            duration,
            userId);
    }
}