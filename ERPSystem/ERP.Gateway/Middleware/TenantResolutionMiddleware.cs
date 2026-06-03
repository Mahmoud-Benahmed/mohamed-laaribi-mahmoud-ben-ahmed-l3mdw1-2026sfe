using ERP.Gateway.Cache;
using ERP.Gateway.Properties;

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

    private static readonly HashSet<string> PlatformAdminRoles =
    [
        TenantRoles.TENANT_SUPPORT,
        TenantRoles.BILLING_MANAGER,
        TenantRoles.SUPER_PLATFORM_ADMIN
    ];

    private static readonly string[] InactiveAllowedPaths =
    [
        "/tenants/branding/",
        "/plans",
        "/auth/me",
        "/tenants/admin"
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
        _logger.LogDebug("=== TenantResolution ===");
        _logger.LogDebug("Path: {Path}", context.Request.Path);
        _logger.LogDebug("IsAuthenticated: {Auth}", context.User?.Identity?.IsAuthenticated);
        _logger.LogDebug("Claims: {Claims}",
            string.Join(", ", context.User?.Claims.Select(c => $"{c.Type}={c.Value}") ?? []));
        _logger.LogDebug("X-Tenant-Id header: {Header}",
            context.Request.Headers["X-Tenant-Id"].FirstOrDefault());

        var path = context.Request.Path;

        // 1. Always skip excluded paths first
        if (ExcludedPaths.Any(p => path.StartsWithSegments(p)))
        {
            await _next(context);
            return;
        }

        bool hasJwt = context.User?.Identity?.IsAuthenticated == true;

        // 2. Platform admin: authenticated, has role, tenantId is null → cross-tenant access

        // Then in InvokeAsync:
        bool isPlatformAdmin = hasJwt
            && PlatformAdminRoles.Any(role => context.User!.IsInRole(role))
            && context.User!.FindFirst("tenantId")?.Value is null or "";

        if (isPlatformAdmin)
        {
            // Optionally allow them to pass an explicit tenant via header for scoped operations
            var overrideTenantId = context.Request.Headers["X-Tenant-Id"].FirstOrDefault();
            if (Guid.TryParse(overrideTenantId, out var adminTenantId))
            {
                var cache = context.RequestServices.GetRequiredService<ITenantCache>();
                var overrideTenant = await cache.GetAsync(adminTenantId);

                if (overrideTenant != null)
                {
                    AttachTenant(context, overrideTenant);
                }
            }

            // Platform admin passes through regardless
            await _next(context);
            return;
        }

        // 3. Regular tenant resolution
        TenantCacheEntry? tenant = null;

        if (hasJwt)
        {
            var tenantIdClaim = context.User!.FindFirst("tenantId")?.Value;

            if (Guid.TryParse(tenantIdClaim, out var parsedTenantId))
            {
                context.Items["tenantId"] = parsedTenantId;
                context.Request.Headers.Remove("X-Tenant-Id");
                context.Request.Headers["X-Tenant-Id"] = parsedTenantId.ToString();

                var cache = context.RequestServices.GetRequiredService<ITenantCache>();
                tenant = await cache.GetAsync(parsedTenantId);

                // ✅ Cache miss — fallback to directory client and re-populate
                if (tenant is null)
                {
                    var directory = context.RequestServices
                        .GetRequiredService<ITenantDirectoryClient>();

                    tenant = await directory.ResolveAsync(parsedTenantId);
                    if (tenant is not null)
                        await cache.SetAsync(tenant);
                }
            }
        }

        // 4. Reject if tenant is required but not resolved
        if (IsTenantRequired(context) && tenant == null)
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

        // 5. Reject inactive tenant
        if (tenant != null && !tenant.IsActive)
        {
            path = context.Request.Path;

            // Tier 1: fully public for inactive tenants — no privilege check
            bool isPublicInactivePath = InactiveAllowedPaths
                .Any(p => path.Value?.StartsWith(p) == true);

            // Tier 2: requires BUY_SUBSCRIPTION privilege
            bool isRenewalAction = IsSubscriptionRenewalEndpoint(context.Request)
                && context.User!.Claims.Any(c =>
                    c.Type == "privilege" && c.Value == Privileges.Users.BUY_SUBSCRIPTION);

            if (isPublicInactivePath || isRenewalAction)
            {
                AttachTenant(context, tenant);
                await _next(context);
                return;
            }

            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new
            {
                code = "TENANT_INACTIVE",
                message = "Your subscription has expired or been deactivated.",
                type = "tenant_inactive"
            });
            return;
        }

        // 6. Attach tenant context
        if (tenant != null)
        {
            AttachTenant(context, tenant);
        }

        await _next(context);
    }

    private static void AttachTenant(HttpContext context, TenantCacheEntry tenant)
    {
        context.Items["tenant"] = tenant;
        context.Items["tenantSlug"] = tenant.Slug;
        context.Request.Headers.Remove("X-Tenant-Slug");
        context.Request.Headers["X-Tenant-Slug"] = tenant.Slug;
        context.Request.Headers.Remove("X-Tenant-Id");
        context.Request.Headers["X-Tenant-Id"] = tenant.TenantId.ToString();
    }

    private static bool IsTenantRequired(HttpContext context)
    {
        var path = context.Request.Path.Value?.ToLowerInvariant();
        if (path == null) return false;
        return TenantRequiredPaths.Any(p => path.StartsWith(p));
    }

    // helper
    private static bool IsSubscriptionRenewalEndpoint(HttpRequest request) =>
        request.Method == HttpMethods.Post
        && request.Path.StartsWithSegments("/tenants")
        && request.Path.Value?.EndsWith("/subscription") == true;
}