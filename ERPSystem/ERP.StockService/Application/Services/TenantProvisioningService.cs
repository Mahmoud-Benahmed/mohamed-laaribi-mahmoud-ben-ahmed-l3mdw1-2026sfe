using ERP.StockService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace ERP.StockService.Application.Services;


public interface ITenantProvisioningService
{
    Task DeleteAllByTenantIdAsync(Guid tenantId);
}

public class TenantProvisioningService : ITenantProvisioningService
{
    private readonly StockDbContext _context;

    public TenantProvisioningService(StockDbContext context)
    {
        _context = context;
    }
    public async Task DeleteAllByTenantIdAsync(Guid tenantId)
    {
        // Lignes deleted via Cascade from their parent Bons
        await _context.BonRetours
            .IgnoreQueryFilters()
            .Where(b => b.TenantId == tenantId)
            .ExecuteDeleteAsync();

        await _context.BonSorties
            .IgnoreQueryFilters()
            .Where(b => b.TenantId == tenantId)
            .ExecuteDeleteAsync();

        await _context.BonEntres
            .IgnoreQueryFilters()
            .Where(b => b.TenantId == tenantId)
            .ExecuteDeleteAsync();

        await _context.JournalStocks
            .IgnoreQueryFilters()
            .Where(j => j.TenantId == tenantId)
            .ExecuteDeleteAsync();

        await _context.BonNumber
            .IgnoreQueryFilters()
            .Where(b => b.TenantId == tenantId)
            .ExecuteDeleteAsync();

        await _context.InvoiceBonSortieMappings
            .Where(m => m.TenantId == tenantId)
            .ExecuteDeleteAsync();

        await _context.ClientCategoryMasterCaches
            .Where(c => c.TenantId == tenantId)
            .ExecuteDeleteAsync();

        await _context.ClientCaches
            .IgnoreQueryFilters()
            .Where(c => c.TenantId == tenantId)
            .ExecuteDeleteAsync();

        await _context.ArticleCaches
            .IgnoreQueryFilters()
            .Where(a => a.TenantId == tenantId)
            .ExecuteDeleteAsync();

        await _context.ArticleCategoryCaches
            .IgnoreQueryFilters()
            .Where(a => a.TenantId == tenantId)
            .ExecuteDeleteAsync();

        await _context.FournisseurCaches
            .IgnoreQueryFilters()
            .Where(f => f.TenantId == tenantId)
            .ExecuteDeleteAsync();
    }
}