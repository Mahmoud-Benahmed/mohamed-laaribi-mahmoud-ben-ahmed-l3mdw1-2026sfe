using ERP.ArticleService.Application.Interfaces;
using ERP.ArticleService.Domain;
using ERP.ArticleService.Infrastructure.Persistence;
using ERP.ArticleService.Infrastructure.Persistence.Seeders;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace ERP.ArticleService.Application.Services;


public interface ITenantProvisioningService
{
    Task ProvisionAsync(Guid tenantId, string slug);
    Task DeleteAllByTenantIdAsync(Guid tenantId);
}

public class TenantProvisioningService : ITenantProvisioningService
{
    private readonly ArticleCodeSeeder _articleCodeSeeder;
    private readonly ArticleDbContext _context;

    public TenantProvisioningService(ArticleCodeSeeder articleCodeSeeder, ArticleDbContext context)
    {
        _articleCodeSeeder = articleCodeSeeder;
        _context = context;
    }

    public async Task ProvisionAsync(Guid tenantId, string slug)
    {
        await _articleCodeSeeder.SeedAsync(tenantId, slug);
    }

    public async Task DeleteAllByTenantIdAsync(Guid tenantId)
    {
        await _context.Articles
            .IgnoreQueryFilters()
            .Where(a => a.TenantId == tenantId)
            .ExecuteDeleteAsync();

        await _context.Categories
            .IgnoreQueryFilters()
            .Where(c => c.TenantId == tenantId)
            .ExecuteDeleteAsync();

        await _context.ArticleCodes
            .IgnoreQueryFilters()
            .Where(c => c.TenantId == tenantId)
            .ExecuteDeleteAsync();
    }
}