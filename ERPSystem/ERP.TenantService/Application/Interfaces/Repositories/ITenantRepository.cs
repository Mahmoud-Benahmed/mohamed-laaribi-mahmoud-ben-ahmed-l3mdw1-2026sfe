using ERP.TenantService.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.TenantService.Application.Interfaces.Repositories;

public interface ITenantRepository
{
    Task<bool> DuplicateExists(string email, string phone, string? slug=null, Guid? excludeId= null);
    Task<bool> ExistsAsync(Guid id, CancellationToken ct = default);
    Task<(List<Tenant> Items, int TotalCount)> GetAllAsync(int page, int pageSize, CancellationToken ct = default);
    Task<List<Tenant>> GetAllActiveAsync(CancellationToken ct = default);
    Task<(List<Tenant> Items, int TotalCount)> GetDeletedAsync(int page, int pageSize, CancellationToken ct = default);
    Task<int> CountAsync(CancellationToken ct = default);
    Task<Tenant?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Tenant?> GetByIdDeletedAsync(Guid id, CancellationToken ct = default);
    Task<Tenant?> GetByIdWithSubscriptionAsync(Guid id, CancellationToken ct = default);
    Task<Tenant?> GetBySlugAsync(string slug, CancellationToken ct = default);
    Task<bool> SubdomainSlugExistsAsync(string slug, Guid? excludeId = null, CancellationToken ct = default);
    Task<bool> EmailExistsAsync(string email, Guid? excludeId = null, CancellationToken ct = default);
    Task<Tenant?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task AddAsync(Tenant tenant, CancellationToken ct = default);
    Task UpdateAsync(Tenant tenant);
    Task SaveChangesAsync(CancellationToken ct = default);
}
