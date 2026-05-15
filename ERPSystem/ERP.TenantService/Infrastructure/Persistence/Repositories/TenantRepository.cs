using ERP.TenantService.Application.Interfaces;
using ERP.TenantService.Domain;
using ERP.TenantService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.TenantService.Infrastructure.Persistence.Repositories;

public class TenantRepository : ITenantRepository
{
    private readonly TenantDbContext _context;

    public TenantRepository(TenantDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Tenant>> GetAllAsync(int page, int pageSize)
    {
        return await _context.Tenants
            .AsNoTracking()
            .OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<int> CountAsync()
    {
        return await _context.Tenants.CountAsync();
    }

    public async Task<Tenant?> GetByIdAsync(Guid id)
    {
        return await _context.Tenants.FindAsync(id);
    }

    public async Task<Tenant?> GetByIdWithSubscriptionAsync(Guid id)
    {
        return await _context.Tenants
            .Include(t => t.Subscription)
            .ThenInclude(s => s!.Plan)
            .FirstOrDefaultAsync(t => t.Id == id);
    }

    public async Task<Tenant?> GetBySubdomainSlugAsync(string slug)
    {
        return await _context.Tenants
            .Include(t => t.Subscription)
            .ThenInclude(s => s!.Plan)
            .FirstOrDefaultAsync(t => t.SubdomainSlug == slug);
    }

    public async Task<bool> SubdomainSlugExistsAsync(string slug, Guid? excludeId = null)
    {
        return await _context.Tenants
            .AnyAsync(t => t.SubdomainSlug == slug && (excludeId == null || t.Id != excludeId));
    }

    public async Task AddAsync(Tenant tenant)
    {
        await _context.Tenants.AddAsync(tenant);
    }

    public Task UpdateAsync(Tenant tenant)
    {
        _context.Tenants.Update(tenant);
        return Task.CompletedTask;
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
