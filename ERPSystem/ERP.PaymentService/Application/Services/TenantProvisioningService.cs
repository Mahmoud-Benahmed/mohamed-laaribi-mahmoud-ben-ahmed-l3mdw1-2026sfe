using Microsoft.EntityFrameworkCore;
using System.Data;

namespace ERP.PaymentService.Application.Services;


public interface ITenantProvisioningService
{
    Task DeleteAllByTenantIdAsync(Guid tenantId);
}

public class TenantProvisioningService : ITenantProvisioningService
{
    private readonly PaymentDbContext _context;

    public TenantProvisioningService(PaymentDbContext context)
    {
        _context = context;
    }
    public async Task DeleteAllByTenantIdAsync(Guid tenantId)
    {
        await _context.Refunds
            .IgnoreQueryFilters()
            .Where(r => r.TenantId == tenantId)
            .ExecuteDeleteAsync();

        await _context.Payments
            .IgnoreQueryFilters()
            .Where(p => p.TenantId == tenantId)
            .ExecuteDeleteAsync();

        await _context.InvoiceCaches
            .IgnoreQueryFilters() // ✅ add for consistency
            .Where(i => i.TenantId == tenantId)
            .ExecuteDeleteAsync();

        await _context.PaymentSequences
            .IgnoreQueryFilters()
            .Where(s => s.TenantId == tenantId)
            .ExecuteDeleteAsync();
    }
}