using ERP.AuthService.Application.Interfaces.Repositories;
using ERP.AuthService.Domain;
using MongoDB.Driver;

namespace ERP.AuthService.Infrastructure.Persistence.Repositories
{
    public class RoleRepository : BaseRepository<Role>, IRoleRepository
    {
        public RoleRepository(MongoDbContext context)
            : base(context, CollectionNames.Roles) { }

        public async Task<Role?> GetByIdAsync(Guid id)
            => await _collection.Find(x => x.Id == id).FirstOrDefaultAsync();

        public async Task<Role?> GetByLibelleAsync(string libelle)
            => await _collection
                .Find(x => x.Libelle == libelle.Trim().ToUpper())
                .FirstOrDefaultAsync();


        public async Task<(List<Role> Items, int TotalCount)> GetAllPagedAsync(int pageNumber, int pageSize)
        {
            pageNumber = Math.Max(pageNumber, 1);
            pageSize = Math.Max(pageSize, 1);
            FilterDefinition<Role> filter = Builders<Role>.Filter.Empty;
            int totalCount = (int)await _collection.CountDocumentsAsync(filter);
            List<Role> items = await _collection.Find(filter)
                .Sort(Builders<Role>.Sort.Ascending(r => r.Libelle))
                .Skip((pageNumber - 1) * pageSize)
                .Limit(pageSize)
                .ToListAsync();
            return (items, totalCount);
        }

        public async Task<List<Role>> GetAllAsync()
            => await _collection
                .Find(Builders<Role>.Filter.Empty)
                .Sort(Builders<Role>.Sort.Ascending(r => r.Libelle))
                .ToListAsync();


        public async Task AddAsync(Role role)
            => await _collection.InsertOneAsync(role);

        public async Task UpdateAsync(Role role)
            => await _collection.ReplaceOneAsync(x => x.Id == role.Id, role);

        public async Task DeleteAsync(Guid id)
            => await _collection.DeleteOneAsync(x => x.Id == id);

        public async Task DeleteAllAsync()
        {
            await _collection.DeleteManyAsync(FilterDefinition<Role>.Empty);
        }

        public async Task<long> CountAsync()
            => await _collection.CountDocumentsAsync(_ => true);
    }
}