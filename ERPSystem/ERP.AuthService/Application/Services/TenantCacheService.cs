using ERP.AuthService.Application.Interfaces.Repositories;
using ERP.AuthService.Application.Interfaces.Services;
using ERP.AuthService.Domain.Cache;
using ERP.AuthService.Infrastructure.Messaging.Events.TenantEvent;

namespace ERP.AuthService.Application.Services;


// ── Implementation ────────────────────────────────────────────────────────────

public sealed class TenantCacheService : ITenantCacheService
{
    private readonly ITenantCacheRepository _repo;
    private readonly ILogger<TenantCacheService> _logger;

    public TenantCacheService(
        ITenantCacheRepository repo,
        ILogger<TenantCacheService> logger)
    {
        _repo = repo;
        _logger = logger;
    }

    public Task<TenantCache?> GetByIdAsync(Guid tenantId)
        => _repo.GetByIdAsync(tenantId);

    public Task<TenantCache?> GetBySlugAsync(string slug)
        => _repo.GetBySlugAsync(slug);

    public async Task SyncCreatedAsync(TenantCreatedEvent e)
    {
        _logger.LogInformation("Syncing created tenant {TenantId} ({Slug})", e.TenantId, e.Slug);

        TenantCache? existing = await _repo.GetByIdAsync(e.TenantId);

        if (existing is not null)
        {
            _logger.LogWarning("Tenant {TenantId} already exists in cache, applying update", e.TenantId);
            existing.ApplyUpdate(e);
            await _repo.UpsertAsync(existing);
        }
        else
        {
            TenantCache tenant = TenantCache.FromEvent(e);
            await _repo.UpsertAsync(tenant);
        }

        _logger.LogInformation("Tenant {TenantId} synced successfully", e.TenantId);
    }

    public async Task SyncUpdatedAsync(TenantUpdatedEvent e)
    {
        _logger.LogInformation("Syncing updated tenant {TenantId} ({Slug})", e.TenantId, e.NewSlug);

        TenantCache? existing = await _repo.GetByIdAsync(e.TenantId);

        TenantCreatedEvent evt = new(
            TenantId: e.TenantId,
            Slug: e.NewSlug,
            IsActive: e.IsActive,
            Name: e.Name,
            Address: e.Address,
            Email: e.Email,
            Phone: e.Phone,
            Currency: e.Currency,
            PrimaryColor: e.PrimaryColor,
            SecondaryColor: e.SecondaryColor
        );

        if (existing is null)
        {
            _logger.LogWarning("Tenant {TenantId} not found in cache during update, creating", e.TenantId);
            await _repo.UpsertAsync(TenantCache.FromEvent(evt));
        }
        else
        {
            existing.ApplyUpdate(evt);
            await _repo.UpsertAsync(existing);
        }

        _logger.LogInformation("Tenant {TenantId} updated successfully", e.TenantId);
    }

    public async Task ActivateAsync(Guid tenantId)
    {
        _logger.LogInformation("Activating tenant {TenantId} in cache", tenantId);
        await _repo.ActivateAsync(tenantId);
    }

    public async Task DeactivateAsync(Guid tenantId)
    {
        _logger.LogInformation("Deactivating tenant {TenantId} in cache", tenantId);
        await _repo.DeactivateAsync(tenantId);
    }

    public async Task DeleteAsync(Guid tenantId)
    {
        _logger.LogInformation("Deleting tenant {TenantId} from cache", tenantId);
        await _repo.DeleteAsync(tenantId);
    }
}