using ERP.AuthService.Domain.Cache;
using ERP.AuthService.Infrastructure.Messaging.Events.TenantEvent;

namespace ERP.AuthService.Application.Interfaces.Services;

using ERP.AuthService.Application.Interfaces.Repositories;
using ERP.AuthService.Domain;

// ── Interface ─────────────────────────────────────────────────────────────────

public interface ITenantCacheService
{
    Task<TenantCache?> GetByIdAsync(Guid tenantId);
    Task<TenantCache?> GetBySlugAsync(string slug);
    Task SyncCreatedAsync(TenantCreatedEvent e);
    Task SyncUpdatedAsync(TenantUpdatedEvent e);
    Task ActivateAsync(Guid tenantId);
    Task DeactivateAsync(Guid tenantId);
    Task DeleteAsync(Guid tenantId);
}
