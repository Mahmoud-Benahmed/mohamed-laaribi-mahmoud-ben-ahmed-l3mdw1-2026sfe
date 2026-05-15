using ERP.TenantService.Domain;

namespace ERP.TenantService.Application.Interfaces;

public interface ITenantSubscriptionRepository
{
    Task<TenantSubscription?> GetByTenantIdAsync(Guid tenantId);
    Task SaveChangesAsync();
}
