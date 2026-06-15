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
    public TenantContext(IHttpContextAccessor? httpContextAccessor = null)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private Guid? _manualTenantId;   // backing field for manually set value

    public Guid? TenantId
    {
        get
        {
            // 1. If manually set (e.g., by Kafka consumer), return that
            if (_manualTenantId.HasValue)
                return _manualTenantId;

            // 2. Otherwise, try to get from HTTP context (web requests)
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null)
                return null;

            // Header first
            var header = httpContext.Request.Headers["X-Tenant-Id"].FirstOrDefault();
            var value = header?.Split(',').FirstOrDefault()?.Trim();
            if (Guid.TryParse(value, out var fromHeader))
                return fromHeader;

            // JWT claim fallback
            var claim = httpContext.User?.FindFirst("tenantId")?.Value;
            if (Guid.TryParse(claim, out var fromClaim))
                return fromClaim;

            return null;
        }
        private set => _manualTenantId = value;   // allow set via method
    }

    public void SetTenantId(Guid tenantId) => TenantId = tenantId;

    public string? Slug =>
        _httpContextAccessor.HttpContext?.Request.Headers["X-Tenant-Slug"].FirstOrDefault();
}