using ERP.InvoiceService.Application.Services;
using System.Text.Json;

namespace ERP.InvoiceService.Infrastructure.Messaging;

public class StockServiceHttpClient : IStockServiceHttpClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<StockServiceHttpClient>? _logger;
    private readonly ITenantContext _tenantContext;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public StockServiceHttpClient(HttpClient httpClient,
        ITenantContext tenantContext,
        ILogger<StockServiceHttpClient>? logger = null)
    {
        _tenantContext = tenantContext;
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <summary>
    /// Gets complete stock status (in stock and out of stock articles)
    /// </summary>
    public async Task<StockStatusResponse> GetStockStatusAsync()
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "stock/articles");

            // Use TenantId directly from ITenantContext
            if (_tenantContext.TenantId.HasValue)
            {
                request.Headers.Add("X-Tenant-Id", _tenantContext.TenantId.Value.ToString());
                _logger?.LogInformation("X-Tenant-Id forwarded: '{TenantId}'", _tenantContext.TenantId.Value);
            }
            else
            {
                _logger?.LogWarning("No TenantId available in context — stock call will be unscoped");
            }

            _logger?.LogInformation("Calling URL: {Url}", _httpClient.BaseAddress + "stock/articles");

            HttpResponseMessage response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            string json = await response.Content.ReadAsStringAsync();
            _logger?.LogInformation("StockService raw JSON: {Json}", json);

            return JsonSerializer.Deserialize<StockStatusResponse>(json, _jsonOptions)
                   ?? new StockStatusResponse();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to get stock status");
            throw;
        }
    }
}