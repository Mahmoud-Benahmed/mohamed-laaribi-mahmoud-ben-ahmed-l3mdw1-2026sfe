using Microsoft.EntityFrameworkCore;

namespace ERP.StockService.Application.Services;


public interface ITenantContext
{
    Guid? TenantId { get; }
    string? Slug { get; }
    void SetTenantId(Guid tenantId);
}
public class TenantContext : ITenantContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    private Guid? _tenantId;

    public TenantContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid? TenantId
    {
        get
        {
            // Explicitly assigned value wins first
            if (_tenantId.HasValue)
                return _tenantId;

            // HTTP header
            var header = _httpContextAccessor.HttpContext?
                .Request.Headers["X-Tenant-Id"]
                .FirstOrDefault();

            var value = header?.Split(',').FirstOrDefault()?.Trim();

            if (Guid.TryParse(value, out var fromHeader))
                return fromHeader;

            // JWT fallback
            var claim = _httpContextAccessor.HttpContext?
                .User?
                .FindFirst("tenantId")
                ?.Value;

            if (Guid.TryParse(claim, out var fromClaim))
                return fromClaim;

            return null;
        }
    }

    public string? Slug =>
        _httpContextAccessor.HttpContext?
            .Request.Headers["X-Tenant-Slug"]
            .FirstOrDefault();

    public void SetTenantId(Guid tenantId)
    {
        _tenantId = tenantId;
    }
}