namespace ERP.ArticleService.Application.Services;

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
            var header = _httpContextAccessor.HttpContext?
                .Request.Headers["X-Tenant-Id"].FirstOrDefault();

            // ✅ handle comma-separated duplicates
            var value = header?.Split(',').FirstOrDefault()?.Trim();

            if (Guid.TryParse(value, out var tenantId))
                return tenantId;

            return null;
        }
    }
    public string? Slug
    {
        get
        {
            return _httpContextAccessor.HttpContext?
                .Request.Headers["X-Tenant-Slug"].FirstOrDefault();
        }
    }
}