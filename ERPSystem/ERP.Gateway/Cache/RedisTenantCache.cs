using ERP.Gateway.Cache;
using StackExchange.Redis;
using System.Text.Json;

public interface ITenantCache
{
    Task<TenantCacheEntry?> GetAsync(string slug);
    Task<TenantCacheEntry?> GetAsync(Guid tenantId);
    Task SetAsync(TenantCacheEntry tenant);
    Task RemoveAsync(string slug);
}

public class RedisTenantCache : ITenantCache
{
    private readonly IDatabase _db;

    public RedisTenantCache(IConnectionMultiplexer redis)
    {
        _db = redis.GetDatabase();
    }

    public async Task<TenantCacheEntry?> GetAsync(string slug)
    {
        var value = await _db.StringGetAsync($"tenant:slug:{slug}");

        if (!value.HasValue)
            return null;

        var json = value.ToString();

        if (string.IsNullOrWhiteSpace(json))
            return null;

        return JsonSerializer.Deserialize<TenantCacheEntry>(json);
    }

    public async Task<TenantCacheEntry?> GetAsync(Guid tenantId)
    {
        var value = await _db.StringGetAsync($"tenant:id:{tenantId}");

        if (!value.HasValue)
            return null;

        var json = value.ToString();

        if (string.IsNullOrWhiteSpace(json))
            return null;

        return JsonSerializer.Deserialize<TenantCacheEntry>(json);
    }


    public async Task SetAsync(TenantCacheEntry tenant)
    {
        var json = JsonSerializer.Serialize(tenant);

        await _db.StringSetAsync(
            $"tenant:slug:{tenant.Slug}",
            json,
            TimeSpan.FromMinutes(30));
    }

    public async Task RemoveAsync(string slug)
    {
        await _db.KeyDeleteAsync($"tenant:slug:{slug}");
    }
}