using ERP.TenantService.Application.Interfaces;
using ERP.TenantService.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.TenantService.Infrastructure.Persistence.Repositories;

public class TenantSubscriptionRepository : ITenantSubscriptionRepository
{
    private readonly TenantDbContext _context;

    public TenantSubscriptionRepository(TenantDbContext context)
    {
        _context = context;
    }

    public async Task<TenantSubscription?> GetByTenantIdAsync(Guid tenantId, CancellationToken ct = default)
    {
        return await _context.TenantSubscriptions
            .Include(s => s.Plan)
            .FirstOrDefaultAsync(s => s.TenantId == tenantId, ct);
    }
    public async Task<List<TenantSubscription>> GetBySubscriptionPlanIdAsync(Guid subscriptionPlanId, CancellationToken ct = default)
    {
        return await _context.TenantSubscriptions
            .Where(s => s.SubscriptionPlanId == subscriptionPlanId)
            .ToListAsync(ct);
    }

    public async Task<List<TenantSubscription>> GetActiveBySubscriptionPlanIdAsync(
        Guid subscriptionPlanId, DateTime asOf, CancellationToken ct = default)
    {
        return await _context.TenantSubscriptions
            .Where(s => s.SubscriptionPlanId == subscriptionPlanId && s.EndDate > asOf)
            .ToListAsync(ct);
    }

    public async Task<List<TenantSubscription>> GetExpiredAsync(DateTime asOf, CancellationToken ct)
    {
        return await _context.TenantSubscriptions
            .Where(s => s.EndDate <= asOf)
            .Include(s => s.Plan)
            .ToListAsync(ct);
    }

    public async Task AddAsync(TenantSubscription subscription, CancellationToken ct = default)
    {
        await _context.TenantSubscriptions.AddAsync(subscription, ct);
    }

    public void Update(TenantSubscription subscription)
    {
        _context.TenantSubscriptions.Update(subscription);
    }

    public async Task SaveChangesAsync( CancellationToken ct = default)
    {
        await _context.SaveChangesAsync(ct);
    }
}
