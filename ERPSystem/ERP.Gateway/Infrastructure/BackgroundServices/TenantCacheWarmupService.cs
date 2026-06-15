using ERP.Gateway.Cache;
using StackExchange.Redis;

namespace ERP.Gateway.Infrastructure.BackgroundServices;

public sealed class TenantCacheWarmupService : BackgroundService
{
    private readonly ITenantDirectoryClient _client;
    private readonly ITenantCache _cache;
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<TenantCacheWarmupService> _logger;

    public TenantCacheWarmupService(
        ITenantDirectoryClient client,
        ITenantCache cache,
        IConnectionMultiplexer redis,
        ILogger<TenantCacheWarmupService> logger)
    {
        _client = client;
        _cache = cache;
        _redis = redis;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        // Give downstream services time to start
        await Task.Delay(TimeSpan.FromSeconds(15), ct);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var tenants = await _client.GetAllActiveAsync(ct);

                // Flush stale keys
                var server = _redis.GetServer(_redis.GetEndPoints().First());
                var keys = server.Keys(pattern: "tenant:*").ToArray();
                if (keys.Length > 0)
                    await _cache.FlushAsync(keys);

                // Repopulate
                foreach (var tenant in tenants)
                    await _cache.SetAsync(tenant);

                _logger.LogInformation(
                    "Tenant cache warmed up with {Count} tenants", tenants.Count);

                return; // ✅ success — exit the loop
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Tenant cache warmup failed — retrying in 10 seconds");

                await Task.Delay(TimeSpan.FromSeconds(10), ct);
            }
        }
    }
}