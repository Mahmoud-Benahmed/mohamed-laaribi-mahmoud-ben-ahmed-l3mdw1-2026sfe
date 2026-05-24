namespace ERP.Gateway.Cache;

using System.Net;
using System.Net.Http.Json;

public interface ITenantDirectoryClient
{
    Task<TenantCacheEntry?> ResolveAsync(string slug);
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

    public async Task<TenantCacheEntry?> ResolveAsync(string slug)
    {
        var response = await _httpClient.GetAsync($"/tenants/slug/{slug}");

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
}