
namespace ERP.Gateway.Cache;

public class TenantCacheEntry
{
    public Guid TenantId { get; set; } = default!;
    public string Slug { get; set; } = default!;
    public bool IsActive { get; set; } = default!;
}