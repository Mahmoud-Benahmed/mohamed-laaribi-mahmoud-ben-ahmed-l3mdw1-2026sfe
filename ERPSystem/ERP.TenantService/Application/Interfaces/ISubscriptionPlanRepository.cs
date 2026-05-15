using ERP.TenantService.Domain;

namespace ERP.TenantService.Application.Interfaces;

public interface ISubscriptionPlanRepository
{
    Task<IEnumerable<SubscriptionPlan>> GetAllAsync();
    Task<SubscriptionPlan?> GetByIdAsync(Guid id);
    Task<bool> CodeExistsAsync(string code, Guid? excludeId = null);
    Task AddAsync(SubscriptionPlan plan);
    Task UpdateAsync(SubscriptionPlan plan);
    Task SaveChangesAsync();
}
