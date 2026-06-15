namespace ERP.AuthService.Infrastructure.ApiClient;

using System.Net;
using System.Net.Http.Json;

public interface ITenantApiClient
{
    Task<TenantSubscriptionResponseDto?> ResolveAsync(Guid? id);
}

public sealed class TenantApiClient: ITenantApiClient
{
    private readonly HttpClient _httpClient;

    public TenantApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<TenantSubscriptionResponseDto?> ResolveAsync(Guid? id)
    {
        var response = await _httpClient.GetAsync($"/tenants/{id}/subscription");

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<TenantSubscriptionResponseDto>();
    }
}

public record TenantSubscriptionResponseDto(
    Guid TenantId,
    DateTimeOffset StartDate,
    DateTimeOffset EndDate,
    SubscriptionPlanResponseDto? Plan);

public record SubscriptionPlanResponseDto(
    Guid Id,
    string Name,
    string Code,
    decimal MonthlyPrice,
    decimal YearlyPrice,
    int MaxUsers,
    int MaxStorageMb,
    bool IsActive);