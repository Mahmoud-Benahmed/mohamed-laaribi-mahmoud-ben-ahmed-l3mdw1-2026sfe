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

    public async Task<IEnumerable<SubscriptionPlan>> GetAllAsync()
    {
        return await _context.SubscriptionPlans
            .AsNoTracking()
            .OrderBy(p => p.MonthlyPrice)
            .ToListAsync();
    }

    public async Task<SubscriptionPlan?> GetByIdAsync(Guid id)
    {
        return await _context.SubscriptionPlans.FindAsync(id);
    }

    public async Task<bool> CodeExistsAsync(string code, Guid? excludeId = null)
    {
        return await _context.SubscriptionPlans
            .AnyAsync(p => p.Code == code && (excludeId == null || p.Id != excludeId));
    }

    public async Task AddAsync(SubscriptionPlan plan)
    {
        await _context.SubscriptionPlans.AddAsync(plan);
    }

    public Task UpdateAsync(SubscriptionPlan plan)
    {
        _context.SubscriptionPlans.Update(plan);
        return Task.CompletedTask;
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
