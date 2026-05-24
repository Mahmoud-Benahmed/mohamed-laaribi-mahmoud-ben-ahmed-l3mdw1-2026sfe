using ERP.ArticleService.Application.Interfaces;
using ERP.ArticleService.Domain;
using ERP.ArticleService.Infrastructure.Persistence.Seeders;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Data;

namespace ERP.ArticleService.Application.Services;


public interface ITenantProvisioningService
{
    Task ProvisionAsync(Guid tenantId, string slug);
}

public class TenantProvisioningService : ITenantProvisioningService
{
    private readonly ArticleCodeSeeder _articleCodeSeeder;

    public TenantProvisioningService(ArticleCodeSeeder articleCodeSeeder)
    {
        _articleCodeSeeder = articleCodeSeeder;
    }

    public async Task ProvisionAsync(Guid tenantId, string slug)
    {
        // ✅ Pass the tenantId explicitly
        await _articleCodeSeeder.SeedAsync(tenantId, slug);
    }
}