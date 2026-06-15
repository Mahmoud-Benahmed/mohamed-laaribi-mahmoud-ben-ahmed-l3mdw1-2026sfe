using ERP.PaymentService.Application.Interfaces.LocalCache;
using ERP.PaymentService.Application.Services;
using ERP.PaymentService.Infrastructure.Messaging.TenantEvent;
using Microsoft.EntityFrameworkCore;

namespace ERP.PaymentService.Infrastructure.Persistence.Repositories.LocalCache;

public class InvoiceCacheRepository : IInvoiceCacheRepository
{
    private readonly PaymentDbContext _context;
    private readonly ITenantContext _tenantContext;

    public InvoiceCacheRepository(PaymentDbContext context, ITenantContext tenantContext)
    {
        _tenantContext = tenantContext;
        _context = context;
    }

    // ✅ fix 4: Id alone is sufficient — it's the primary key
    public async Task<InvoiceCache?> GetByIdAsync(Guid invoiceId)
    {
        return await _context.InvoiceCaches
            .FirstOrDefaultAsync(ic => ic.Id == invoiceId);
    }

    public async Task<(List<InvoiceCache> Items, int TotalCount)> GetByClientIdAsync(
        Guid clientId, int pageNumber, int pageSize)
    {
        // ✅ fix 2: guard clamps
        if (pageNumber < 1) pageNumber = 1;
        if (pageSize < 1) pageSize = 10;
        if (pageSize > 100) pageSize = 100;

        IQueryable<InvoiceCache> query = _context.InvoiceCaches
            .AsNoTracking()                          // ✅ fix 1
            .Where(ic => ic.ClientId == clientId)
            .OrderByDescending(ic => ic.LastUpdated); // ✅ fix 3

        int totalCount = await query.CountAsync();

        List<InvoiceCache> items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<(List<InvoiceCache> Items, int TotalCount)> GetByStatusAsync(
        InvoiceStatus status, int pageNumber, int pageSize)
    {
        // ✅ fix 2: guard clamps
        if (pageNumber < 1) pageNumber = 1;
        if (pageSize < 1) pageSize = 10;
        if (pageSize > 100) pageSize = 100;

        IQueryable<InvoiceCache> query = _context.InvoiceCaches
            .AsNoTracking()                          // ✅ fix 1
            .Where(ic => ic.Status == status)
            .OrderByDescending(ic => ic.LastUpdated); // ✅ fix 3

        int totalCount = await query.CountAsync();

        List<InvoiceCache> items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task AddAsync(InvoiceCache cache)
    {
        await _context.InvoiceCaches.AddAsync(cache);
    }

    public Task UpdateAsync(InvoiceCache cache)
    {
        _context.InvoiceCaches.Update(cache);
        return Task.CompletedTask;
    }

    public async Task SaveChangesAsync()
        => await _context.SaveChangesAsync();

    public async Task<(List<InvoiceCache> Items, int TotalCount)> GetPagedAsync(
        int pageNumber, int pageSize, string? search = null)
    {
        if (pageNumber < 1) pageNumber = 1;
        if (pageSize < 1) pageSize = 10;
        if (pageSize > 100) pageSize = 100;

        IQueryable<InvoiceCache> query = _context.InvoiceCaches.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            string q = search.Trim().ToLower();
            query = query.Where(ic =>
                ic.InvoiceNumber.ToLower().Contains(q) ||
                ic.ClientId.ToString().Contains(q));
        }

        int totalCount = await query.CountAsync();

        List<InvoiceCache> items = await query
            .OrderByDescending(ic => ic.LastUpdated)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }
}