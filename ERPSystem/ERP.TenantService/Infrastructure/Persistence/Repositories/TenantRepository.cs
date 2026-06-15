using ERP.TenantService.Application.Interfaces;
using ERP.TenantService.Application.Interfaces.Repositories;
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
    public async Task<bool> ExistsAsync(Guid id, CancellationToken ct = default)
        => await _context.Tenants.AnyAsync(t => t.Id == id, ct);
    public async Task<bool> DuplicateExists(string email, string phone, string? slug=null, Guid? excludeId= null)
    {
        var query = _context.Tenants.Where(t =>
            t.Email.ToLower() == email.ToLower() ||
            t.Phone.ToLower() == phone.ToLower() ||
            (!string.IsNullOrEmpty(slug) && t.Slug.ToLower() == slug.ToLower())
        );


        if (excludeId.HasValue)
            query = query.Where(a => a.Id != excludeId.Value);

        return await query.AnyAsync();
    }

    public async Task<(List<Tenant> Items, int TotalCount)> GetAllAsync(
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Max(pageSize, 1);

        var query = _context.Tenants
            .AsNoTracking()
            .Include(t => t.Subscription)
                .ThenInclude(s => s.Plan)
            .OrderByDescending(t => t.CreatedAt);

        var total = await query.CountAsync(ct);

        var tenants = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (tenants, total);
    }

    public async Task<List<Tenant>> GetAllActiveAsync(CancellationToken ct = default)
    {
        var tenants = await _context.Tenants
                             .AsNoTracking()
                             .Where(t=> t.IsActive)
                             .OrderByDescending(t => t.CreatedAt)
                             .ToListAsync(ct);
        return tenants;
    }

    public async Task<(List<Tenant> Items, int TotalCount)> GetDeletedAsync(int page, int pageSize, CancellationToken ct = default)
    {
        var query = _context.Tenants
            .IgnoreQueryFilters()
            .Where(t => t.IsDeleted)
            .AsNoTracking();

        var total = await query.CountAsync(ct);

        var tenants = await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        // Load subscriptions separately
        var tenantIds = tenants.Select(t => t.Id).ToList();

        var subscriptions = await _context.TenantSubscriptions
            .IgnoreQueryFilters()
            .Include(s => s.Plan)
            .Where(s => tenantIds.Contains(s.TenantId))
            .AsNoTracking()
            .ToListAsync(ct);

        // Manual fixup
        var subMap = subscriptions.ToDictionary(s => s.TenantId);
        foreach (var tenant in tenants)
        {
            if (subMap.TryGetValue(tenant.Id, out var sub))
                tenant.SetSubscription(sub); // see note below
        }

        return (tenants, total);
    }

    public async Task<int> CountAsync(CancellationToken ct = default)
    {
        return await _context.Tenants.CountAsync(ct);
    }

    public async Task<Tenant?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Tenants.FindAsync(id, ct);
    }
    public async Task<Tenant?> GetByIdDeletedAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Tenants
                             .IgnoreQueryFilters()
                             .FirstOrDefaultAsync(t=> t.Id == id, ct);
    }

    public async Task<Tenant?> GetByIdWithSubscriptionAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Tenants
            .Include(t => t.Subscription)
            .ThenInclude(s => s!.Plan)
            .FirstOrDefaultAsync(t => t.Id == id, ct);
    }

    public async Task<Tenant?> GetBySlugAsync(string slug, CancellationToken ct = default)
    {
        return await _context.Tenants
            .Include(t => t.Subscription)
            .ThenInclude(s => s!.Plan)
            .FirstOrDefaultAsync(t => t.Slug == slug, ct);
    }

    public async Task<Tenant?> GetByEmailAsync(string email, CancellationToken ct = default)
    {
        return await _context.Tenants
            .FirstOrDefaultAsync(t => t.Email.ToLower() 
                                    == email.ToLower(), ct);
    }

    public async Task<bool> SubdomainSlugExistsAsync(string slug, Guid? excludeId = null, CancellationToken ct = default)
    {
        return await _context.Tenants
            .AnyAsync(t => t.Slug == slug
                && (excludeId == null || t.Id != excludeId), ct);
    }

    public async Task<bool> EmailExistsAsync(string email, Guid? excludeId = null, CancellationToken ct = default)
    {
        return await _context.Tenants
            .AnyAsync(t => t.Email == email
                && (excludeId == null || t.Id != excludeId), ct);
    }

    public async Task AddAsync(Tenant tenant, CancellationToken ct = default)
    {
        await _context.Tenants.AddAsync(tenant, ct);
    }

    public Task UpdateAsync(Tenant tenant)
    {
        _context.Tenants.Update(tenant);
        return Task.CompletedTask;
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        await _context.SaveChangesAsync(ct);
    }
}
