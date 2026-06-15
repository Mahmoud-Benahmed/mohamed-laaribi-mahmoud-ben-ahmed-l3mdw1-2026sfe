
using System.Text.Json.Serialization;

namespace ERP.Gateway.Cache;

public class TenantCacheEntry
{

    [JsonPropertyName("id")]
    public Guid TenantId { get; set; }
    public string Slug { get; set; } = default!;
    public bool IsActive { get; set; } = default!;
}