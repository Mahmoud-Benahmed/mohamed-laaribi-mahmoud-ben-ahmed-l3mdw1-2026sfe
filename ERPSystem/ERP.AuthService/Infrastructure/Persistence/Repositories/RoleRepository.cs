using ERP.AuthService.Application.Interfaces.Repositories;
using ERP.AuthService.Application.Services;
using ERP.AuthService.Domain;
using MongoDB.Driver;

namespace ERP.AuthService.Infrastructure.Persistence.Repositories;

public class RoleRepository : BaseRepository<Role>, IRoleRepository
{
    public RoleRepository(MongoDbContext context)
        : base(context, CollectionNames.Roles) {}

    public async Task<Role?> GetByIdAsync(Guid id)
        => await _collection.Find(WithTenant(x => x.Id == id)).FirstOrDefaultAsync();

    public async Task<Role?> GetByLibelleAsync(string libelle)
    {
        var filter = Builders<Role>.Filter.And(
            ScopeFilter,
            Builders<Role>.Filter.Eq(x => x.Libelle, libelle.Trim().ToUpper())
        );
        return await _collection.Find(filter).FirstOrDefaultAsync();
    }


    public async Task<(List<Role> Items, int TotalCount)> GetAllPagedAsync(int pageNumber, int pageSize)
    {
        pageNumber = Math.Max(pageNumber, 1);
        pageSize = Math.Max(pageSize, 1);

        FilterDefinition<Role> filter = ScopeFilter;

        int totalCount = (int)await _collection.CountDocumentsAsync(filter);
        List<Role> items = await _collection.Find(filter)
            .Sort(Builders<Role>.Sort.Ascending(r => r.Libelle))
            .Skip((pageNumber - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync();
        return (items, totalCount);
    }

    public async Task<List<Role>> GetAllAsync()
    {
        var items = await _collection
            .Find(ScopeFilter)
            .SortBy(r => r.Libelle)
            .ToListAsync();

        return items;
    }

    public async Task AddAsync(Role role)
        => await _collection.InsertOneAsync(role);

    public async Task UpdateAsync(Role role)
    {
        // Platform admin → only update platform roles (TenantId = null)
        // Tenant user → only update their own tenant's roles
        var filter = _hasTenant
            ? WithTenant(x => x.Id == role.Id)
            : WithGlobal(x => x.Id == role.Id);  // ✅ platform admin scoped to null-tenant roles

        await _collection.ReplaceOneAsync(filter, role);
    }

    public async Task DeleteAsync(Guid id)
    {
        var filter = _hasTenant
            ? WithTenant(x => x.Id == id)
            : WithGlobal(x => x.Id == id);  // ✅ platform admin scoped to null-tenant roles

        await _collection.DeleteOneAsync(filter);
    }

    public async Task<long> CountAsync()
        => await _collection.CountDocumentsAsync(GlobalFilter);
}