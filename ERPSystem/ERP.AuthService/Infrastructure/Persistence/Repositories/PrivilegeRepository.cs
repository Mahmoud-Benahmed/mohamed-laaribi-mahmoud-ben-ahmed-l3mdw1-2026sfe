using ERP.AuthService.Application.Interfaces.Repositories;
using ERP.AuthService.Domain;
using MongoDB.Driver;

namespace ERP.AuthService.Infrastructure.Persistence.Repositories;

class PrivilegeRepository : BaseRepository<Privilege>, IPrivilegeRepository
{
    public PrivilegeRepository(MongoDbContext context)
        : base(context, CollectionNames.Privileges) { }

    // ✅ ID-based — no filter needed, used by seeders
    public async Task<Privilege?> GetByIdAsync(Guid id)
        => await _collection.Find(x => x.Id == id).FirstOrDefaultAsync();

    // ✅ used by seeders — roleId + controleId is specific enough
    public async Task<Privilege?> GetByRoleIdAndControleIdAsync(Guid roleId, Guid controleId)
        => await _collection
            .Find(x => x.RoleId == roleId && x.ControleId == controleId)
            .FirstOrDefaultAsync();

    // ✅ used by JWT generation — roleId is specific, add IsGranted filter here
    public async Task<List<Privilege>> GetByRoleIdAsync(Guid roleId)
        => await _collection
            .Find(x => x.RoleId == roleId)
            .ToListAsync();

    // ✅ scope filter for API use
    public async Task<List<Privilege>> GetByControleIdAsync(Guid controleId)
        => await _collection
            .Find(WithTenant(x => x.ControleId == controleId))
            .ToListAsync();

    public async Task AddAsync(Privilege privilege)
        => await _collection.InsertOneAsync(privilege);

    // ✅ tenant guard on mutations
    public async Task UpdateAsync(Privilege privilege)
        => await _collection.ReplaceOneAsync(
            WithTenant(x => x.Id == privilege.Id), privilege);

    public async Task DeleteAsync(Guid id)
        => await _collection.DeleteOneAsync(WithTenant(x => x.Id == id));

    public async Task DeleteByRoleIdAsync(Guid roleId)
        => await _collection.DeleteManyAsync(
            WithTenant(x => x.RoleId == roleId));

    public async Task DeleteAllAsync()
        => await _collection.DeleteManyAsync(TenantFilter);

    public async Task DeleteByControleIdAsync(Guid controleId)
        => await _collection.DeleteManyAsync(
            WithTenant(x => x.ControleId == controleId));

    public async Task<long> CountAsync()
        => await _collection.CountDocumentsAsync(ScopeFilter);
}