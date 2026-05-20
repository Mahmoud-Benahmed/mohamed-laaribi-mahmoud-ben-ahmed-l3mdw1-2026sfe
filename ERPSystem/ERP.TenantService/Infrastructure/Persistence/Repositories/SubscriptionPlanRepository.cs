using ERP.TenantService.Application.Interfaces;
using ERP.TenantService.Domain;
using ERP.TenantService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.TenantService.Infrastructure.Persistence.Repositories;

public class SubscriptionPlanRepository : ISubscriptionPlanRepository
{
    private readonly TenantDbContext _context;

    public SubscriptionPlanRepository(TenantDbContext context)
    {
        _context = context;
    }

    public async Task<(List<SubscriptionPlan> Items, int TotalCount)> GetAllAsync(int page, int pageSize, CancellationToken ct = default)
    {
        var query = _context.SubscriptionPlans
            .AsNoTracking()
            .OrderBy(p => p.MonthlyPrice);

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task<SubscriptionPlan?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.SubscriptionPlans.FindAsync(id, ct);
    }
    public async Task<SubscriptionPlan?> GetByCodeAsync(string code, CancellationToken ct = default)
    {
        var normalized = code.ToUpperInvariant();
        return await _context.SubscriptionPlans
            .FirstOrDefaultAsync(p => p.Code == normalized, ct);
    }

    public async Task<bool> CodeExistsAsync(string code, Guid? excludeId = null, CancellationToken ct = default)
    {
        return await _context.SubscriptionPlans
            .AnyAsync(p => p.Code == code && (excludeId == null || p.Id != excludeId), ct);
    }

    public async Task AddAsync(SubscriptionPlan plan, CancellationToken ct = default)
    {
        await _context.SubscriptionPlans.AddAsync(plan, ct);
    }

    public Task UpdateAsync(SubscriptionPlan plan)
    {
        _context.SubscriptionPlans.Update(plan);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(SubscriptionPlan plan)
    {
        _context.SubscriptionPlans.Remove(plan);
        return Task.CompletedTask;
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        await _context.SaveChangesAsync(ct);
    }
}
