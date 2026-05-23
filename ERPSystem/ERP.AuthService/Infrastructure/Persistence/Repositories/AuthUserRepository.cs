using ERP.AuthService.Application.DTOs.AuthUser;
using ERP.AuthService.Application.Interfaces.Repositories;
using ERP.AuthService.Domain;
using MongoDB.Bson;
using MongoDB.Driver;

namespace ERP.AuthService.Infrastructure.Persistence.Repositories
{
    public class AuthUserRepository : BaseRepository<AuthUser>, IAuthUserRepository
    {
        public AuthUserRepository(MongoDbContext context)
            : base(context, CollectionNames.Users){}
        public async Task AddAsync(AuthUser user)
            => await _collection.InsertOneAsync(user);

        public async Task<AuthUser?> GetByLoginAsync(string login)
            => await _collection.Find(x => x.Login == login && !x.IsDeleted).FirstOrDefaultAsync();

        public async Task<AuthUser?> GetByEmailAsync(string email)
            => await _collection.Find(WithTenant(x => x.Email == email && x.IsActive && !x.IsDeleted)).FirstOrDefaultAsync();

        public async Task<AuthUser?> GetByIdAsync(Guid id)
            => await _collection.Find(x => x.Id == id && !x.IsDeleted).FirstOrDefaultAsync();

        public async Task<AuthUser?> GetByDeletedIdAsync(Guid id)
            => await _collection.Find(x => x.Id == id && x.IsDeleted).FirstOrDefaultAsync();

        public async Task<(List<AuthUser> Items, int TotalCount)> GetAllAsync(int pageNumber, int pageSize, Guid? excludeId = null)
            => await GetPagedAsync(pageNumber, pageSize, excludeId);

        public async Task<(List<AuthUser>, int)> GetPagedByStatusAsync(bool status, int pageNumber, int pageSize, Guid? excludeId = null)
        {
            FilterDefinition<AuthUser> filter = Builders<AuthUser>.Filter.Where(u => u.IsActive == status && !u.IsDeleted);

            return await GetPagedAsync(pageNumber, pageSize, excludeId, filter);
        }

        public async Task<(List<AuthUser>, int)> GetPagedByRoleAsync(Guid role, int pageNumber, int pageSize, Guid? excludeId = null)
        {
            FilterDefinition<AuthUser> filter = Builders<AuthUser>.Filter.Where(u => u.RoleId == role && u.IsActive);

            return await GetPagedAsync(pageNumber, pageSize, excludeId, filter);
        }

        public async Task<(List<AuthUser>, int)> GetDeletedPagedAsync(int pageNumber, int pageSize, Guid? excludeId = null)
        {
            return await GetPagedAsync(pageNumber, pageSize, excludeId, includeDeleted: true);
        }

        public async Task<bool> ExistsByEmailAsync(string email)
            => await _collection.Find(x => x.Email == email && x.IsActive && !x.IsDeleted).AnyAsync();

        public async Task<bool> ExistsByLoginAsync(string login)
            => await _collection.Find(x => x.Login == login && x.IsActive && !x.IsDeleted).AnyAsync();

        public async Task<AuthUser?> UpdateAsync(AuthUser user)
        {
            var filter = _hasTenant
                ? Builders<AuthUser>.Filter.And(
                    Builders<AuthUser>.Filter.Eq(x => x.Id, user.Id),
                    Builders<AuthUser>.Filter.Eq("TenantId", _tenantId!.Value))
                : Builders<AuthUser>.Filter.Eq(x => x.Id, user.Id);

            ReplaceOneResult result = await _collection.ReplaceOneAsync(filter, user);
            return result.ModifiedCount > 0 ? user : null;
        }

        public async Task<int> CountAsync()
            => (int)await _collection.CountDocumentsAsync(x => x.IsActive && !x.IsDeleted);

        public async Task<int> CountByTenantIdAsync(Guid tenantId)
            => (int)await _collection.CountDocumentsAsync(WithTenant(x => x.TenantId == tenantId && x.IsActive && !x.IsDeleted));


        public async Task<int> CountByStatusAsync(bool status) =>
            (int)await _collection.CountDocumentsAsync(WithTenant(
                    x => x.IsActive == status
                ));

        public async Task<UserStatsDto> GetStatsAsync(Guid? excludeId = null)
        {
            var pipeline = new List<BsonDocument>();

            // Stage 1: scope filter
            if (_hasTenant && _tenantId.HasValue)
            {
                pipeline.Add(new BsonDocument("$match",
                    new BsonDocument("TenantId",
                        new BsonBinaryData(_tenantId.Value, GuidRepresentation.Standard))));
            }
            else
            {
                pipeline.Add(new BsonDocument("$match", new BsonDocument()));  // platform admin — all
            }

            // Stage 2: exclude specific ID
            if (excludeId.HasValue)
            {
                pipeline.Add(new BsonDocument("$match",
                    new BsonDocument("_id",
                        new BsonDocument("$ne",
                            new BsonBinaryData(excludeId.Value, GuidRepresentation.Standard)))));
            }

            // Stage 3: group and count
            pipeline.Add(new BsonDocument("$group", new BsonDocument
            {
                { "_id", BsonNull.Value },
                { "total", new BsonDocument("$sum",
                    new BsonDocument("$cond", new BsonArray
                    {
                        new BsonDocument("$eq", new BsonArray { "$IsDeleted", false }), 1, 0
                    }))},
                { "active", new BsonDocument("$sum",
                    new BsonDocument("$cond", new BsonArray
                    {
                        new BsonDocument("$and", new BsonArray
                        {
                            new BsonDocument("$eq", new BsonArray { "$IsDeleted", false }),
                            new BsonDocument("$eq", new BsonArray { "$IsActive", true })
                        }), 1, 0
                    }))},
                { "deactivated", new BsonDocument("$sum",
                    new BsonDocument("$cond", new BsonArray
                    {
                        new BsonDocument("$and", new BsonArray
                        {
                            new BsonDocument("$eq", new BsonArray { "$IsDeleted", false }),
                            new BsonDocument("$eq", new BsonArray { "$IsActive", false })
                        }), 1, 0
                    }))},
                { "deleted", new BsonDocument("$sum",
                    new BsonDocument("$cond", new BsonArray
                    {
                        new BsonDocument("$eq", new BsonArray { "$IsDeleted", true }), 1, 0
                    }))}
            }));

            var result = await _collection.Aggregate<BsonDocument>(pipeline).FirstOrDefaultAsync();

            if (result == null)
                return new UserStatsDto();

            return new UserStatsDto
            {
                TotalUsers = result["total"].ToInt32(),
                ActiveUsers = result["active"].ToInt32(),
                DeactivatedUsers = result["deactivated"].ToInt32(),
                DeletedUsers = result["deleted"].ToInt32()
            };
        }


        private async Task<(List<AuthUser> Items, int TotalCount)> GetPagedAsync(
            int pageNumber,
            int pageSize,
            Guid? excludeId = null,
            FilterDefinition<AuthUser>? filter = null,
            SortDefinition<AuthUser>? sort = null,
            bool includeDeleted = false
        )
        {
            pageNumber = Math.Max(pageNumber, 1);
            pageSize = Math.Max(pageSize, 1);

            var filters = new List<FilterDefinition<AuthUser>>
            {
                ScopeFilter  // ✅ replaces manual TenantFilter insertion
            };

            if (!includeDeleted)
                filters.Add(Builders<AuthUser>.Filter.Where(u => !u.IsDeleted));
            else
                filters.Add(Builders<AuthUser>.Filter.Where(u => u.IsDeleted));

            if (filter != null)
                filters.Add(filter);

            if (excludeId.HasValue)
                filters.Add(Builders<AuthUser>.Filter.Where(u => u.Id != excludeId.Value));

            var finalFilter = Builders<AuthUser>.Filter.And(filters);
            sort ??= Builders<AuthUser>.Sort.Ascending(u => u.CreatedAt);

            int totalCount = (int)await _collection.CountDocumentsAsync(finalFilter);
            var items = await _collection
                .Find(finalFilter)
                .Sort(sort)
                .Skip((pageNumber - 1) * pageSize)
                .Limit(pageSize)
                .ToListAsync();

            return (items, totalCount);
        }
    }
}
