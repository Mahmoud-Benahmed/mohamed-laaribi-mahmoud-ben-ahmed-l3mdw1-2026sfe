using ERP.Gateway.Cache;

namespace ERP.Gateway.Middleware;

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
        "/favicon.ico",
        "/admin"
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
        "/payment",
        "/plans"
    ];

    private static readonly string[] TenantOptionalAuthPaths =
    [
        "/auth/login",
        "/auth/register",
        "/auth/refresh"
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
            // =========================
            // POST-AUTH: JWT ONLY
            // =========================
            var tenantId = context.User.FindFirst("tenantId")?.Value;

            if (Guid.TryParse(tenantId, out var parsedTenantId))
            {
                ITenantCache cache = context.RequestServices.GetRequiredService<ITenantCache>();
                tenant = await cache.GetAsync(parsedTenantId);
            }
        }
        else
        {
            // =========================
            // PRE-AUTH: HEADER ONLY
            // =========================
            if (context.Request.Headers.TryGetValue("X-Tenant", out var slug))
            {
                ITenantCache cache = context.RequestServices.GetRequiredService<ITenantCache>();

                tenant = await cache.GetAsync(slug!);

                if (tenant == null)
                {
                    ITenantDirectoryClient client =
                        context.RequestServices.GetRequiredService<ITenantDirectoryClient>();

                    tenant = await client.ResolveAsync(slug!);

                    if (tenant != null)
                        await cache.SetAsync(tenant);
                }
            }
        }

        bool tenantRequired = IsTenantRequired(context);

        TenantCacheEntry? tenantCacheEntry = null;

        string? jwtTenantId = context.User?.FindFirst("tenantId")?.Value;

        if (Guid.TryParse(jwtTenantId, out var parsedJwtTenantId))
        {
            ITenantCache cache = context.RequestServices.GetRequiredService<ITenantCache>();
            tenant = await cache.GetAsync(parsedJwtTenantId.ToString());
        }

        bool isAuthBypass = TenantOptionalAuthPaths.Any(p =>
                                context.Request.Path.Value?.StartsWith(p, StringComparison.OrdinalIgnoreCase) == true);

        if (tenantRequired && tenant == null && !isAuthBypass)
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

        // 3. Enforce ONLY if endpoint requires tenant
        if (tenantRequired && tenant == null)
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

        // 4. If tenant exists, validate it
        if (tenant != null && !tenant.IsActive)
        {
            context.Response.StatusCode = 403;

            await context.Response.WriteAsJsonAsync(new
            {
                statusCode = 403,
                code = "TENANT_INACTIVE",
                message = $"Tenant '{tenant.Slug}' is inactive"
            });

            return;
        }

        // 5. Attach tenant context (ONLY if exists)
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