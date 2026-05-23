namespace ERP.AuthService.Application.Services;

public interface ITenantContext
{
    Guid? TenantId { get; }
    string? Slug { get; }
}
public class TenantContext : ITenantContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public TenantContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid? TenantId
    {
        get
        {
            var ctx = _httpContextAccessor.HttpContext;
            if (ctx == null) return null;

            // Try header first (forwarded by gateway)
            var header = ctx.Request.Headers["X-Tenant-Id"].FirstOrDefault();
            if (Guid.TryParse(header, out var fromHeader))
                return fromHeader;

            // Fallback to JWT claim
            var claim = ctx.User?.FindFirst("tenantId")?.Value;
            if (Guid.TryParse(claim, out var fromClaim))
                return fromClaim;

            return null;
        }
    }
    public string? Slug
    {
        get
        {
            return _httpContextAccessor.HttpContext?.Items["tenantSlug"]?.ToString();
        }
    }
}