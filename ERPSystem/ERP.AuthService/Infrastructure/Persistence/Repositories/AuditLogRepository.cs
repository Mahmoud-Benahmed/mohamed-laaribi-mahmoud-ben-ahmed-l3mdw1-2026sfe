using ERP.AuthService.Application.Interfaces.Repositories;
using ERP.AuthService.Domain;
using ERP.AuthService.Domain.Logger;
using MongoDB.Driver;

namespace ERP.AuthService.Infrastructure.Persistence.Repositories
{
    public class AuditLogRepository : BaseRepository<AuditLog>, IAuditLogRepository
    {
        public AuditLogRepository(MongoDbContext context)
            : base(context, CollectionNames.AuditLogs) { }

        public async Task AddAsync(AuditLog log)
            => await _collection.InsertOneAsync(log);

        public async Task<List<AuditLog>> GetByUserAsync(Guid userId, int pageNumber, int pageSize)
        {
            FilterDefinition<AuditLog> filter = Builders<AuditLog>.Filter.Or(
                Builders<AuditLog>.Filter.Eq(x => x.PerformedBy, userId),
                Builders<AuditLog>.Filter.Eq(x => x.TargetUserId, userId)
            );
            return await _collection.Find(filter)
                .SortByDescending(x => x.Timestamp)
                .Skip((pageNumber - 1) * pageSize)
                .Limit(pageSize)
                .ToListAsync();
        }

        public async Task<List<AuditLog>> GetAllAsync(int pageNumber, int pageSize)
            => await _collection.Find(_ => true)
                .SortByDescending(x => x.Timestamp)
                .Skip((pageNumber - 1) * pageSize)
                .Limit(pageSize)
                .ToListAsync();

        public async Task<long> CountAsync()
            => await _collection.CountDocumentsAsync(_ => true);

        // AuditLogRepository
        public async Task ClearAsync()
            => await _collection.DeleteManyAsync(FilterDefinition<AuditLog>.Empty);
    }
}