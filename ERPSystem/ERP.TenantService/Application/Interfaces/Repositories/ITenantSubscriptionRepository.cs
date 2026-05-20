using ERP.TenantService.Domain;

namespace ERP.TenantService.Application.Interfaces;

public interface ITenantSubscriptionRepository
{
    Task<List<TenantSubscription>> GetExpiredAsync(DateTime asOf, CancellationToken ct);
    Task<TenantSubscription?> GetByTenantIdAsync(Guid tenantId, CancellationToken ct = default);
    Task<List<TenantSubscription>> GetBySubscriptionPlanIdAsync(Guid subscriptionPlanId, CancellationToken ct = default);
    Task<List<TenantSubscription>> GetActiveBySubscriptionPlanIdAsync(Guid subscriptionPlanId, DateTime asOf, CancellationToken ct = default);
    Task AddAsync(TenantSubscription subscription, CancellationToken ct = default);
    void Update(TenantSubscription subscription);

    Task SaveChangesAsync(CancellationToken ct = default);
}
