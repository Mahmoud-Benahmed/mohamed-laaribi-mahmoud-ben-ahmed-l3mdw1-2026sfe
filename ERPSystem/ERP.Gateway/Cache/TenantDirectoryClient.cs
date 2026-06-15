namespace ERP.Gateway.Cache;

using System.Net;
using System.Net.Http.Json;

public interface ITenantDirectoryClient
{
    Task<TenantCacheEntry?> ResolveAsync(Guid tenantId);
    Task<List<TenantCacheEntry>> GetAllActiveAsync(CancellationToken ct = default);
}

public class TenantDirectoryClient : ITenantDirectoryClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<TenantDirectoryClient> _logger;

    public TenantDirectoryClient(
        HttpClient httpClient,
        ILogger<TenantDirectoryClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<TenantCacheEntry?> ResolveAsync(Guid tenantId)
    {
        var response = await _httpClient.GetAsync($"/tenants/{tenantId}");

        return response.StatusCode switch
        {
            HttpStatusCode.NotFound =>
                null,

            var code when response.IsSuccessStatusCode =>
                await response.Content.ReadFromJsonAsync<TenantCacheEntry>(),

            _ => throw new HttpRequestException(
                $"TenantService returned {response.StatusCode}")
        };
    }

    public async Task<List<TenantCacheEntry>> GetAllActiveAsync(CancellationToken ct = default)
    {
        var response = await _httpClient.GetAsync("/tenants/active", ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Failed to fetch active tenants: {Status}", response.StatusCode);
            return [];
        }

        return await response.Content
            .ReadFromJsonAsync<List<TenantCacheEntry>>(cancellationToken: ct) ?? [];
    }
}