namespace ERP.ClientService.Middleware;

public class TenantResolver
{
    private readonly RequestDelegate _next;

    public TenantResolver(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var tenant = context.Request.Headers["X-Tenant"].FirstOrDefault();
        if (!string.IsNullOrEmpty(tenant))
        {
            context.Items["TenantSlug"] = tenant;
        }
        await _next(context);
    }
}