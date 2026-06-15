using ERP.AuthService.Application.Interfaces.Repositories;
using ERP.AuthService.Domain;
using MongoDB.Driver;

namespace ERP.AuthService.Infrastructure.Persistence.Repositories
{
    public class ControleRepository : BaseRepository<Controle>, IControleRepository
    {
        public ControleRepository(MongoDbContext context)
            : base(context, CollectionNames.Controles) { }

        public async Task<List<Controle>> GetByIdsAsync(IEnumerable<Guid> ids)
        {
            var filter = Builders<Controle>.Filter.And(
                ScopeFilter,
                Builders<Controle>.Filter.In(x => x.Id, ids)
            );
            return await _collection.Find(filter).ToListAsync();
        }

        public async Task<Controle?> GetByIdAsync(Guid id)
            => await _collection.Find(WithTenant(x => x.Id == id)).FirstOrDefaultAsync();

        public async Task<Controle?> GetByLibelleAsync(string libelle)
            => await _collection
                .Find(WithTenant(x => x.Libelle == libelle.Trim().ToUpper()))
                .FirstOrDefaultAsync();


        public async Task<(List<Controle> Items, int TotalCount)> GetAllPagedAsync(int pageNumber, int pageSize)
        {
            return await GetPagedAsync(pageNumber, pageSize);
        }

        public async Task<List<Controle>> GetAllAsync()
        {
            return await _collection
                .Find(ScopeFilter)
                .SortBy(c => c.Libelle)
                .ToListAsync();
        }
        public async Task<bool> DuplicateExists(string libelle, Guid? excludeId = null)
        {
            var filter = WithTenant(x =>
                x.Libelle.ToLower() == libelle.ToLower()
            );

            if (excludeId.HasValue)
                filter = Builders<Controle>.Filter.And(filter, Builders<Controle>.Filter.Ne(x => x.Id, excludeId.Value));

            return await _collection.Find(filter).AnyAsync();
        }

        public async Task<(List<Controle> Items, int TotalCount)> GetByCategoryAsync(
            string category,
            int pageNum,
            int pageSize)
        {
            var categoryFilter = Builders<Controle>.Filter.Eq(x => x.Category, category);
            var filter = Builders<Controle>.Filter.And(ScopeFilter, categoryFilter);

            return await GetPagedAsync(pageNum, pageSize, filter,
                collation: new Collation("en", strength: CollationStrength.Secondary));
        }

        public async Task AddAsync(Controle controle)
            => await _collection.InsertOneAsync(controle);

        public async Task UpdateAsync(Controle controle)
        {
            var filter = WithTenant(x => x.Id == controle.Id);
            await _collection.ReplaceOneAsync(filter, controle);
        }

        public async Task DeleteAsync(Guid id)
        {
            var filter = WithTenant(x => x.Id == id);
            await _collection.DeleteOneAsync(filter);
        }

        public async Task DeleteAllAsync()
        {
            await _collection.DeleteManyAsync(TenantFilter);
        }

        public async Task<int> CountAsync()
        {
            return (int)await _collection.CountDocumentsAsync(ScopeFilter);
        }

        private async Task<(List<Controle> Items, int TotalCount)> GetPagedAsync(
            int pageNumber,
            int pageSize,
            FilterDefinition<Controle>? customFilter = null,
            SortDefinition<Controle>? sort = null,
            Collation? collation = null
        )
        {
            pageNumber = Math.Max(pageNumber, 1);
            pageSize = Math.Max(pageSize, 1);

            // ✅ Start with ScopeFilter as base
            var filters = new List<FilterDefinition<Controle>> { ScopeFilter };

            if (customFilter != null)
                filters.Add(customFilter);

            var finalFilter = filters.Count > 0
                ? Builders<Controle>.Filter.And(filters)
                : Builders<Controle>.Filter.Empty;

            sort ??= Builders<Controle>.Sort.Ascending(c => c.Libelle);

            var totalCount = (int)await _collection.CountDocumentsAsync(
                finalFilter, new CountOptions { Collation = collation });

            var items = await _collection
                .Find(finalFilter, new FindOptions { Collation = collation })
                .Sort(sort)
                .Skip((pageNumber - 1) * pageSize)
                .Limit(pageSize)
                .ToListAsync();

            return (items, totalCount);
        }
    }
}