using ERP.Gateway.Cache;

namespace ERP.Gateway.Middleware;

public sealed class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TenantResolutionMiddleware> _logger;

    public TenantResolutionMiddleware(
        RequestDelegate next,
        ILogger<TenantResolutionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Headers.ContainsKey("X-Api-Key"))
        {
            await _next(context);
            return;
        }

        string? slug = ExtractSlug(context.Request.Host.Host);

        if (string.IsNullOrWhiteSpace(slug))
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

        ITenantCache cache = context.RequestServices.GetRequiredService<ITenantCache>();
        TenantCacheEntry? tenant = await cache.GetAsync(slug);

        // Fallback → TenantService
        if (tenant == null)
        {
            _logger.LogInformation(
                "Tenant cache miss for slug '{Slug}'",
                slug);

            ITenantDirectoryClient client = context.RequestServices.GetRequiredService<ITenantDirectoryClient>();

            tenant = await client.ResolveAsync(slug);

            if (tenant == null)
            {
                context.Response.StatusCode = 404;

                await context.Response.WriteAsJsonAsync(new
                {
                    statusCode = 404,
                    code = "TENANT_NOT_FOUND",
                    message = $"Tenant '{slug}' not found"
                });

                return;
            }

            await cache.SetAsync(tenant);
        }

        if (!tenant.IsActive)
        {
            context.Response.StatusCode = 403;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new
            {
                statusCode = 403,
                code = "TENANT_INACTIVE",
                message = $"Tenant '{slug}' is currently inactive"
            });
            return;
        }

        // Canonical tenant context
        context.Items["tenantId"] = tenant.TenantId.ToString();
        context.Items["tenant"] = tenant;

        // Optional trusted internal propagation
        context.Request.Headers["X-Tenant-Id"] = tenant.TenantId.ToString();

        await _next(context);
    }

    private static string? ExtractSlug(string host)
    {
        string[] parts = host.Split('.');

        // acme.erp.local
        if (parts.Length >= 3)
            return parts[0].ToLowerInvariant();

        return null;
    }
}