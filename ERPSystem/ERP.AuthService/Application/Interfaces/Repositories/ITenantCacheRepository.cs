using ERP.AuthService.Domain.Cache;

namespace ERP.AuthService.Application.Interfaces.Repositories;

public interface ITenantCacheRepository
{
    Task<TenantCache?> GetByIdAsync(Guid tenantId);
    Task<TenantCache?> GetBySlugAsync(string slug);
    Task<bool> ExistsAsync(Guid tenantId);
    Task UpsertAsync(TenantCache tenant);
    Task ActivateAsync(Guid tenantId);
    Task DeactivateAsync(Guid tenantId);
    Task DeleteAsync(Guid tenantId);
}