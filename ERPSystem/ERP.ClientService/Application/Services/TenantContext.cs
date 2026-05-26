using Microsoft.EntityFrameworkCore;

namespace ERP.ClientService.Application.Services;


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
            // 1. Header first (forwarded by gateway)
            var header = _httpContextAccessor.HttpContext?
                .Request.Headers["X-Tenant-Id"].FirstOrDefault();

            var value = header?.Split(',').FirstOrDefault()?.Trim();

            if (Guid.TryParse(value, out var fromHeader))
                return fromHeader;

            // 2. Fallback to JWT claim (direct calls / dev)
            var claim = _httpContextAccessor.HttpContext?  // ✅ via HttpContext
                .User?.FindFirst("tenantId")?.Value;

            if (Guid.TryParse(claim, out var fromClaim))
                return fromClaim;

            return null;
        }
    }

    public string? Slug =>
        _httpContextAccessor.HttpContext?
            .Request.Headers["X-Tenant-Slug"].FirstOrDefault();
}