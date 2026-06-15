using ERP.InvoiceService.Application.Interfaces;
using ERP.InvoiceService.Domain.LocalCache.Tenant;
using Microsoft.EntityFrameworkCore;

namespace ERP.InvoiceService.Infrastructure.Persistence.Repositories;

public class TenantCacheRepository : ITenantCacheRepository
{
    private readonly InvoiceDbContext _context;

    public TenantCacheRepository(InvoiceDbContext context) => _context = context;

    public async Task<TenantCache?> GetByIdAsync(Guid? id) =>
        await _context.TenantCaches.FindAsync(id);

    public async Task<TenantCache?> GetBySlugAsync(string slug) =>
        await _context.TenantCaches.FirstOrDefaultAsync(t => t.Slug == slug);

    public async Task<List<TenantCache>> GetAllAsync() =>
        await _context.TenantCaches.ToListAsync();

    public async Task AddAsync(TenantCache tenant) =>
        await _context.TenantCaches.AddAsync(tenant);

    public Task DeleteAsync(TenantCache tenant)
    {
        _context.TenantCaches.Remove(tenant);
        return Task.CompletedTask;
    }

    public async Task SaveChangesAsync() =>
        await _context.SaveChangesAsync();
}