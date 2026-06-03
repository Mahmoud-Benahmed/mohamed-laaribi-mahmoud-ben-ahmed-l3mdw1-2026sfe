using ERP.TenantService.Domain;

namespace ERP.TenantService.Application.Interfaces.Repositories;

public interface ISubscriptionPlanRepository
{
    Task<(List<SubscriptionPlan> Items, int TotalCount)> GetAllAsync(int page, int pageSize, CancellationToken ct = default);
    Task<(List<SubscriptionPlan> Items, int TotalCount)> GetActivePlansAsync(int page, int pageSize, CancellationToken ct = default);
    Task<SubscriptionPlan?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<SubscriptionPlan?> GetByCodeAsync(string code, CancellationToken ct = default);
    Task<bool> CodeExistsAsync(string code, Guid? excludeId = null, CancellationToken ct = default);
    Task AddAsync(SubscriptionPlan plan, CancellationToken ct = default);
    Task UpdateAsync(SubscriptionPlan plan);
    Task DeleteAsync(SubscriptionPlan plan);

    Task SaveChangesAsync(CancellationToken ct = default);
}
