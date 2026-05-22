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
            var value = ctx?.Items["tenantId"]?.ToString();

            return Guid.TryParse(value, out var id) ? id : null;
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