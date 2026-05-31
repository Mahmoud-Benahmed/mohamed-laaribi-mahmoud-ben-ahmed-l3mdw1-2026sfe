using ERP.InvoiceService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace ERP.InvoiceService.Application.Services;


public interface ITenantProvisioningService
{
    Task DeleteAllByTenantIdAsync(Guid tenantId);
}

public class TenantProvisioningService : ITenantProvisioningService
{
    private readonly InvoiceDbContext _context;

    public TenantProvisioningService(InvoiceDbContext context)
    {
        _context = context;
    }
    public async Task DeleteAllByTenantIdAsync(Guid tenantId)
    {
        // Cache tables — no FK dependencies
        await _context.TenantCaches
            .Where(t => t.TenantId == tenantId)
            .ExecuteDeleteAsync();

        // InvoiceItems deleted via Cascade from Invoices
        await _context.Invoices
            .IgnoreQueryFilters()
            .Where(a => a.TenantId == tenantId)
            .ExecuteDeleteAsync();

        await _context.InvoiceSequences
            .IgnoreQueryFilters()
            .Where(a => a.TenantId == tenantId)
            .ExecuteDeleteAsync();

        await _context.ArticleCaches
            .IgnoreQueryFilters()
            .Where(c => c.TenantId == tenantId)
            .ExecuteDeleteAsync();

        await _context.ArticleCategoryCaches
            .IgnoreQueryFilters()
            .Where(c => c.TenantId == tenantId)
            .ExecuteDeleteAsync();

        await _context.ClientCategoryAssignments
            .IgnoreQueryFilters()
            .Where(ca => ca.Client.TenantId == tenantId)
            .ExecuteDeleteAsync();

        await _context.ClientCategoryMasterCaches
            .IgnoreQueryFilters()
            .Where(c => c.TenantId == tenantId)
            .ExecuteDeleteAsync();

        await _context.ClientCaches
            .IgnoreQueryFilters()
            .Where(c => c.TenantId == tenantId)
            .ExecuteDeleteAsync();
    }
}