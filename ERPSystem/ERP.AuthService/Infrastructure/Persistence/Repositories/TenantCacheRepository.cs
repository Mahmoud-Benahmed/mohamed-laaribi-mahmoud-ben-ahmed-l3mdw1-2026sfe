using ERP.AuthService.Application.Interfaces.Repositories;
using ERP.AuthService.Domain.Cache;
using MongoDB.Driver;

namespace ERP.AuthService.Infrastructure.Persistence.Repositories;

public sealed class TenantCacheRepository : BaseRepository<TenantCache>, ITenantCacheRepository
{
    public TenantCacheRepository(MongoDbContext context)
        : base(context, CollectionNames.TenantsCache) { }

    // ── Read ──────────────────────────────────────────────────────────────────

    public async Task<TenantCache?> GetByIdAsync(Guid tenantId)
        => await _collection
            .Find(Builders<TenantCache>.Filter.Eq(t => t.Id, tenantId))
            .FirstOrDefaultAsync();

    public async Task<TenantCache?> GetBySlugAsync(string slug)
        => await _collection
            .Find(Builders<TenantCache>.Filter.Eq(t => t.Slug, slug))
            .FirstOrDefaultAsync();

    public async Task<bool> ExistsAsync(Guid tenantId)
        => await _collection
            .Find(Builders<TenantCache>.Filter.Eq(t => t.Id, tenantId))
            .AnyAsync();

    // ── Write ─────────────────────────────────────────────────────────────────

    public async Task UpsertAsync(TenantCache tenant)
    {
        var filter = Builders<TenantCache>.Filter.Eq(t => t.Id, tenant.Id);
        await _collection.ReplaceOneAsync(
            filter,
            tenant,
            new ReplaceOptions { IsUpsert = true });
    }

    public async Task ActivateAsync(Guid tenantId)
    {
        var filter = Builders<TenantCache>.Filter.Eq(t => t.Id, tenantId);
        var update = Builders<TenantCache>.Update.Set(t => t.IsActive, true);
        await _collection.UpdateOneAsync(filter, update);
    }

    public async Task DeactivateAsync(Guid tenantId)
    {
        var filter = Builders<TenantCache>.Filter.Eq(t => t.Id, tenantId);
        var update = Builders<TenantCache>.Update.Set(t => t.IsActive, false);
        await _collection.UpdateOneAsync(filter, update);
    }

    public async Task DeleteAsync(Guid tenantId)
    {
        var filter = Builders<TenantCache>.Filter.Eq(t => t.Id, tenantId);
        await _collection.DeleteOneAsync(filter);
    }
}