using System.Net.Http.Json;
using ERP.AuthService.Application.Interfaces;

namespace ERP.AuthService.Infrastructure.Http;

public class TenantServiceClient : ITenantServiceClient
{
    private readonly HttpClient _httpClient;

    public TenantServiceClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<string?> GetSlugByIdAsync(Guid tenantId)
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<TenantResponseDto>($"tenants/{tenantId}");
            return response?.SubdomainSlug;
        }
        catch
        {
            return null;
        }
    }
}

public record TenantResponseDto(string SubdomainSlug);