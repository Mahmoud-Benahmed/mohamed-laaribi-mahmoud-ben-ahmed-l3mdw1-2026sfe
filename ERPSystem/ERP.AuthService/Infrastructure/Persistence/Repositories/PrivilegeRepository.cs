using ERP.AuthService.Application.Interfaces.Repositories;
using ERP.AuthService.Domain;
using MongoDB.Driver;

namespace ERP.AuthService.Infrastructure.Persistence.Repositories
{
    public class PrivilegeRepository : BaseRepository<Privilege>, IPrivilegeRepository
    {
        public PrivilegeRepository(MongoDbContext context)
            : base(context, CollectionNames.Privileges) { }

        public async Task<Privilege?> GetByIdAsync(Guid id)
            => await _collection.Find(x => x.Id == id).FirstOrDefaultAsync();

        public async Task<Privilege?> GetByRoleIdAndControleIdAsync(Guid roleId, Guid controleId)
            => await _collection
                .Find(x => x.RoleId == roleId && x.ControleId == controleId)
                .FirstOrDefaultAsync();

        public async Task<List<Privilege>> GetByRoleIdAsync(Guid roleId)
            => await _collection.Find(x => x.RoleId == roleId).ToListAsync();

        public async Task<List<Privilege>> GetByControleIdAsync(Guid controleId)
            => await _collection.Find(x => x.ControleId == controleId).ToListAsync();

        public async Task AddAsync(Privilege privilege)
            => await _collection.InsertOneAsync(privilege);

        public async Task UpdateAsync(Privilege privilege)
            => await _collection.ReplaceOneAsync(x => x.Id == privilege.Id, privilege);

        public async Task DeleteAsync(Guid id)
            => await _collection.DeleteOneAsync(x => x.Id == id);

        public async Task DeleteAllAsync()
        {
            await _collection.DeleteManyAsync(FilterDefinition<Privilege>.Empty);
        }

        public async Task DeleteByControleIdAsync(Guid controleId)
            => await _collection.DeleteManyAsync(x => x.ControleId == controleId);

        public async Task<long> CountAsync()
            => await _collection.CountDocumentsAsync(_ => true);
    }
}