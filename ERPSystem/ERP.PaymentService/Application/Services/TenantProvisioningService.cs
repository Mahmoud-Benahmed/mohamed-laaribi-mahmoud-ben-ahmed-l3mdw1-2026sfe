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
        // PaymentInvoices must go before Payments (FK constraint)
        await _context.PaymentsInvoices
            .IgnoreQueryFilters()
            .Where(pi => pi.TenantId == tenantId)
            .ExecuteDeleteAsync();

        // RefundLines are owned by RefundRequest — EF cascades automatically
        await _context.Refunds
            .IgnoreQueryFilters()
            .Where(r => r.TenantId == tenantId)
            .ExecuteDeleteAsync();

        await _context.Payments
            .IgnoreQueryFilters()
            .Where(p => p.TenantId == tenantId)
            .ExecuteDeleteAsync();

        await _context.InvoiceCaches
            .IgnoreQueryFilters()
            .Where(i => i.TenantId == tenantId)
            .ExecuteDeleteAsync();

        await _context.PaymentSequences
            .IgnoreQueryFilters()
            .Where(s => s.TenantId == tenantId)
            .ExecuteDeleteAsync();
    }
}