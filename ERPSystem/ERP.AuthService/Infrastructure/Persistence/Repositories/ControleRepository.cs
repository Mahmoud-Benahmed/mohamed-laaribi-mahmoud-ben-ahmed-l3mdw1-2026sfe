using ERP.AuthService.Application.Interfaces.Repositories;
using ERP.AuthService.Domain;
using MongoDB.Driver;

namespace ERP.AuthService.Infrastructure.Persistence.Repositories
{
    public class ControleRepository : BaseRepository<Controle>,IControleRepository
    {
        public ControleRepository(MongoDbContext context)
            : base(context, CollectionNames.Controles) {}
        public async Task<List<Controle>> GetByIdsAsync(IEnumerable<Guid> ids)
        {
            FilterDefinition<Controle> filter = Builders<Controle>.Filter.In(x => x.Id, ids);
            return await _collection.Find(filter).ToListAsync();
        }

        public async Task<Controle?> GetByIdAsync(Guid id)
            => await _collection.Find(x => x.Id == id).FirstOrDefaultAsync();

        public async Task<Controle?> GetByLibelleAsync(string libelle)
            => await _collection
                .Find(x => x.Libelle == libelle.Trim().ToUpper())
                .FirstOrDefaultAsync();

        public Task<(List<Controle> Items, int TotalCount)> GetAllPagedAsync(int pageNumber, int pageSize)
        {
            return GetPagedAsync(pageNumber, pageSize);
        }

        public async Task<List<Controle>> GetAllAsync()
        {
            return await _collection.Find(FilterDefinition<Controle>.Empty).ToListAsync();
        }

        public async Task<(List<Controle> Items, int TotalCount)> GetByCategoryAsync(string category, int pageNum, int pageSize)
        {
            FilterDefinition<Controle> filter = Builders<Controle>.Filter.Eq(x => x.Category, category);

            return await GetPagedAsync(pageNum, pageSize, filter, collation: new Collation("en", strength: CollationStrength.Secondary));
        }

        public async Task AddAsync(Controle controle)
            => await _collection.InsertOneAsync(controle);

        public async Task UpdateAsync(Controle controle)
            => await _collection.ReplaceOneAsync(x => x.Id == controle.Id, controle);

        public async Task DeleteAsync(Guid id)
            => await _collection.DeleteOneAsync(x => x.Id == id);

        public async Task DeleteAllAsync()
        {
            await _collection.DeleteManyAsync(FilterDefinition<Controle>.Empty);
        }

        public async Task<int> CountAsync()
        {
            return (int)await _collection.CountDocumentsAsync(FilterDefinition<Controle>.Empty);
        }


        private async Task<(List<Controle> Items, int TotalCount)> GetPagedAsync(
            int pageNumber,
            int pageSize,
            FilterDefinition<Controle>? filter = null,
            SortDefinition<Controle>? sort = null,
            Collation? collation = null
        )
        {
            pageNumber = Math.Max(pageNumber, 1);
            pageSize = Math.Max(pageSize, 1);

            List<FilterDefinition<Controle>> filters = new List<FilterDefinition<Controle>>();

            // custom filter
            if (filter != null)
                filters.Add(filter);

            FilterDefinition<Controle> finalFilter = filters.Count > 0
                                    ? Builders<Controle>.Filter.And(filters)
                                    : Builders<Controle>.Filter.Empty;

            sort ??= Builders<Controle>.Sort.Ascending(c => c.Libelle);

            FindOptions findOptions = new FindOptions { Collation = collation };

            int totalCount = (int)await _collection.CountDocumentsAsync(
                finalFilter, new CountOptions { Collation = collation });

            List<Controle> items = await _collection
                .Find(finalFilter, new FindOptions { Collation = collation })
                .Sort(sort)
                .Skip((pageNumber - 1) * pageSize)
                .Limit(pageSize)
                .ToListAsync();

            return (items, totalCount);
        }
    }
}