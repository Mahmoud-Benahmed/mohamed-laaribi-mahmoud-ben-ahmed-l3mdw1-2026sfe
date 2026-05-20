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
        try
        {
            HttpResponseMessage response =
                await _httpClient.GetAsync($"/tenants/slug/{slug}");

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                _logger.LogWarning(
                    "Tenant slug '{Slug}' not found",
                    slug);

                return null; // VALID: business case
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "TenantService returned {StatusCode} for slug '{Slug}'",
                    response.StatusCode,
                    slug);

                throw new HttpRequestException(
                    $"TenantService returned {response.StatusCode}");
            }

            TenantCacheEntry? tenant =
                await response.Content.ReadFromJsonAsync<TenantCacheEntry>();

            return tenant;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(
                ex,
                "HTTP failure while resolving tenant '{Slug}'",
                slug);

            throw;
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(
                ex,
                "Timeout while resolving tenant '{Slug}'",
                slug);

            throw;
        }
    }
}