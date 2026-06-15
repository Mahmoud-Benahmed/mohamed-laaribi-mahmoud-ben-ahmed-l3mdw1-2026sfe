using Confluent.Kafka;
using ERP.InvoiceService.Application.Services;
using InvoiceService.Application.Interfaces;
using InvoiceService.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using static InvoiceService.Application.Interfaces.IInvoiceRepository;

namespace ERP.InvoiceService.Infrastructure.Persistence;

public class InvoiceRepository : IInvoiceRepository
{
    private readonly InvoiceDbContext _context;
    private readonly ITenantContext _tenantContext;

    public InvoiceRepository(InvoiceDbContext context, ITenantContext tenantContext)
    {
        _context = context;
        _tenantContext = tenantContext;
    }

    public async Task<List<Invoice>> GetOverdueAsync(DateTime asOf)
        => await _context.Invoices
            .IgnoreQueryFilters()           // ← add this
            .Include(i => i.Items)
            .AsSplitQuery()
            .Where(i => !i.IsDeleted
                     && i.Status == InvoiceStatus.UNPAID
                     && i.DueDate <= asOf)
            .ToListAsync();

    public async Task<bool> PenaltyExistsForInvoiceAsync(string originalInvoiceNumber)
    {
        return await _context.Invoices
            .AnyAsync(i => i.OriginalInvoiceNumber == originalInvoiceNumber
                        && !i.IsDeleted);
    }

    public async Task<bool> PenaltyExistsForPeriodAsync(
    string originalInvoiceNumber,
    DateTime asOf,
    int duePeriod) // ← dynamic, not hardcoded
    {
        DateTime periodStart = asOf.AddDays(-duePeriod);

        return await _context.Invoices
               .IgnoreQueryFilters()
               .AnyAsync(i => i.OriginalInvoiceNumber == originalInvoiceNumber
                           && i.CreatedAt >= periodStart
                           && !i.IsDeleted);
    }

    // ── Existing queries ─────────────────────────────────────────────────────

    public async Task<Invoice?> GetByIdAsync(Guid id)
    {
        return await _context.Invoices
            .Include(i => i.Items)
            .AsSplitQuery()
            .FirstOrDefaultAsync(i => i.Id == id);
    }

    public async Task<Invoice?> GetByIdDeletedAsync(Guid id)
    {
        return await _context.Invoices.IgnoreQueryFilters()
            .Where(i=> i.TenantId == _tenantContext.TenantId)
            .Include(i => i.Items)
            .AsSplitQuery()
            .FirstOrDefaultAsync(i => i.Id == id);
    }

    public async Task<Invoice?> GetByInvoiceNumberAsync(string invoiceNumber)
    {
        return await _context.Invoices
            .Include(i => i.Items)
            .AsSplitQuery()
            .FirstOrDefaultAsync(i => i.InvoiceNumber == invoiceNumber);
    }

    public async Task<(List<Invoice> Items, int TotalCount)> GetAllAsync(
        int pageNumber,
        int pageSize,
        bool includeDeleted = false)
    {
        if (pageNumber < 1) pageNumber = 1;
        if (pageSize < 1) pageSize = 10;
        if (pageSize > 100) pageSize = 100;

        IQueryable<Invoice> query = includeDeleted
            ? _context.Invoices.IgnoreQueryFilters()
            : _context.Invoices;

        query = query.AsNoTracking();

        int totalCount = await query.CountAsync();

        List<Invoice> items = await query
            .Include(i => i.Items)
            .AsSplitQuery()
            .OrderByDescending(i => i.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<(List<Invoice> Items, int TotalCount)> GetByClientIdAsync(
        Guid clientId,
        int pageNumber,
        int pageSize)
    {
        if (pageNumber < 1) pageNumber = 1;
        if (pageSize < 1) pageSize = 10;
        if (pageSize > 100) pageSize = 100;

        IQueryable<Invoice> query = _context.Invoices
            .AsNoTracking()
            .Where(i => i.ClientId == clientId);

        int totalCount = await query.CountAsync();

        List<Invoice> items = await query
            .Include(i => i.Items)
            .AsSplitQuery()
            .OrderByDescending(i => i.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<(List<Invoice> Items, int TotalCount)> GetByStatusAsync(
        InvoiceStatus status,
        int pageNumber,
        int pageSize)
    {
        if (pageNumber < 1) pageNumber = 1;
        if (pageSize < 1) pageSize = 10;
        if (pageSize > 100) pageSize = 100;

        IQueryable<Invoice> query = _context.Invoices
            .AsNoTracking()
            .Where(i => i.Status == status);

        int totalCount = await query.CountAsync();

        List<Invoice> items = await query
            .Include(i => i.Items)
            .AsSplitQuery()
            .OrderByDescending(i => i.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }
    public async Task SaveChangesAsync()
    => await _context.SaveChangesAsync();
    public Task AddAsync(Invoice invoice)
    {
        _context.Invoices.Add(invoice);
        return Task.CompletedTask;
    }

    public async Task UpdateAsync(Invoice invoice)
    {
        await _context.InvoiceItems
            .Where(i => i.InvoiceId == invoice.Id)
            .ExecuteDeleteAsync();

        foreach (EntityEntry<InvoiceItem>? entry in _context.ChangeTracker.Entries<InvoiceItem>().ToList())
        {
            entry.State = EntityState.Detached;
        }

        foreach (InvoiceItem item in invoice.Items)
        {
            _context.InvoiceItems.Add(item);
        }
    }

    public async Task<bool> ExistsByInvoiceNumberAsync(string invoiceNumber)
    {
        return await _context.Invoices
            .IgnoreQueryFilters()
            .AnyAsync(i => i.InvoiceNumber == invoiceNumber);
    }

    // ── Stats queries ─────────────────────────────────────────────────────────

    /// <summary>
    /// Projects only the header-level fields needed for stats aggregation.
    /// Items are intentionally excluded — no Include() — keeping the query lean.
    /// The global soft-delete query filter applies automatically (non-deleted only).
    /// </summary>
    public async Task<List<InvoiceStatProjection>> GetStatsProjectionAsync()
    {
        return await _context.Invoices
            .AsNoTracking()
            .Select(i => new InvoiceStatProjection
            {
                Id = i.Id,
                Status = i.Status,
                InvoiceDate = i.InvoiceDate,
                DueDate = i.DueDate,
                TotalHT = i.TotalHT,
                TotalTTC = i.TotalTTC,
                TotalTVA = i.TotalTVA,
                ClientId = i.ClientId,
                ClientFullName = i.ClientFullName
            })
            .ToListAsync();
    }

    /// <summary>
    /// Counts soft-deleted invoices.
    /// IgnoreQueryFilters() is required to lift the global IsDeleted filter.
    /// </summary>
    public async Task<int> GetDeletedCountAsync()
    {
        return await _context.Invoices
            .IgnoreQueryFilters()
            .CountAsync(i => i.IsDeleted);
    }

    public async Task<List<Invoice>> GetByClientIdAsNoTrackingAsync(Guid clientId)
    {
        return await _context.Invoices
            .AsNoTracking()
            .Include(i => i.Items)
            .AsSplitQuery()
            .Where(i => i.ClientId == clientId)
            .ToListAsync();
    }
}