using ERP.Gateway.Cache;

public sealed class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TenantResolutionMiddleware> _logger;

    private static readonly string[] ExcludedPaths =
    [
        "/health",
        "/health/live",
        "/health/ready",
        "/swagger",
        "/favicon.ico"
    ];

    private static readonly string[] TenantRequiredPaths =
    [
        "/users",
        "/clients",
        "/invoices",
        "/stock",
        "/orders",
        "/audit",
        "/articles",
        "/fournisseurs",
        "/payment"
    ];

    public TenantResolutionMiddleware(
        RequestDelegate next,
        ILogger<TenantResolutionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // 1. Skip system endpoints
        if (ExcludedPaths.Any(p => context.Request.Path.StartsWithSegments(p)))
        {
            await _next(context);
            return;
        }

        bool hasJwt = context.User?.Identity?.IsAuthenticated == true;
        TenantCacheEntry? tenant = null;

        if (hasJwt)
        {
            // POST-AUTH: resolve tenant from JWT claim only
            var tenantId = context.User!.FindFirst("tenantId")?.Value;
            if (Guid.TryParse(tenantId, out var parsedTenantId))
            {
                var cache = context.RequestServices.GetRequiredService<ITenantCache>();
                tenant = await cache.GetAsync(parsedTenantId);
            }
        }
        // ✅ No X-Tenant header resolution at all — tenant always comes from JWT

        bool tenantRequired = IsTenantRequired(context);

        // Authenticated users with tenantId claim satisfy tenant requirement
        bool tenantSatisfied = tenant != null ||
            (hasJwt && !string.IsNullOrEmpty(context.User?.FindFirst("tenantId")?.Value));

        if (tenantRequired && !tenantSatisfied)
        {
            context.Response.StatusCode = 400;
            await context.Response.WriteAsJsonAsync(new
            {
                statusCode = 400,
                code = "TENANT_001",
                message = "Unable to resolve tenant from request."
            });
            return;
        }

        // Reject inactive tenant
        if (tenant != null && !tenant.IsActive)
        {
            context.Response.StatusCode = 403;
            await context.Response.WriteAsJsonAsync(new
            {
                statusCode = 403,
                code = "TENANT_INACTIVE",
                message = $"Tenant '{tenant.Slug}' is inactive."
            });
            return;
        }

        // Attach tenant context if resolved
        if (tenant != null)
        {
            context.Items["tenantId"] = tenant.TenantId;
            context.Items["tenantSlug"] = tenant.Slug;
            context.Items["tenant"] = tenant;
            context.Request.Headers["X-Tenant-Id"] = tenant.TenantId.ToString();
            context.Request.Headers["X-Tenant-Slug"] = tenant.Slug;
        }

        await _next(context);
    }

    private static bool IsTenantRequired(HttpContext context)
    {
        var path = context.Request.Path.Value?.ToLowerInvariant();
        if (path == null) return false;
        return TenantRequiredPaths.Any(p => path.StartsWith(p));
    }
}