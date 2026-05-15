using ERP.TenantService.Domain;

namespace ERP.TenantService.Application.Interfaces;

public interface ITenantRepository
{
    Task<IEnumerable<Tenant>> GetAllAsync(int page, int pageSize);
    Task<int> CountAsync();
    Task<Tenant?> GetByIdAsync(Guid id);
    Task<Tenant?> GetByIdWithSubscriptionAsync(Guid id);
    Task<Tenant?> GetBySubdomainSlugAsync(string slug);
    Task<bool> SubdomainSlugExistsAsync(string slug, Guid? excludeId = null);
    Task AddAsync(Tenant tenant);
    Task UpdateAsync(Tenant tenant);
    Task SaveChangesAsync();
}
