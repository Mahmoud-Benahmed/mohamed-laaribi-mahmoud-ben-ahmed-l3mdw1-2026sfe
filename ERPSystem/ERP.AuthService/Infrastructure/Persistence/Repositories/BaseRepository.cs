using MongoDB.Bson;
using MongoDB.Driver;
using System.Linq.Expressions;

namespace ERP.AuthService.Infrastructure.Persistence.Repositories;

public interface ITenantFilterable
{
    Guid? TenantId { get; }
}

public abstract class BaseRepository<T> where T : class
{
    protected readonly IMongoCollection<T> _collection;
    private readonly Guid? _tenantId;
    private readonly bool _hasTenant;
    private readonly bool _isTenantFilterable;

    protected BaseRepository(MongoDbContext context, string collectionName)
    {
        _collection = context.Collection<T>(collectionName);
        _tenantId = context.TenantId;
        _hasTenant = context.HasTenant;
        _isTenantFilterable = typeof(ITenantFilterable).IsAssignableFrom(typeof(T));
    }

    // tenant-scoped queries
    protected FilterDefinition<T> TenantFilter
    {
        get
        {
            if (!_hasTenant || !_isTenantFilterable)
                return Builders<T>.Filter.Empty;

            return Builders<T>.Filter.Eq("TenantId", _tenantId!.Value);
        }
    }

    // global-only queries (SuperAdmin, admin panel)
    protected FilterDefinition<T> GlobalFilter
    {
        get
        {
            if (!_isTenantFilterable)
                return Builders<T>.Filter.Empty;

            return Builders<T>.Filter.Eq("TenantId", BsonNull.Value);
        }
    }

    protected FilterDefinition<T> WithTenant(FilterDefinition<T> filter)
        => Builders<T>.Filter.And(TenantFilter, filter);

    protected FilterDefinition<T> WithTenant(Expression<Func<T, bool>> predicate)
        => Builders<T>.Filter.And(TenantFilter, Builders<T>.Filter.Where(predicate));

    protected FilterDefinition<T> WithGlobal(FilterDefinition<T> filter)
        => Builders<T>.Filter.And(GlobalFilter, filter);

    protected FilterDefinition<T> WithGlobal(Expression<Func<T, bool>> predicate)
        => Builders<T>.Filter.And(GlobalFilter, Builders<T>.Filter.Where(predicate));

    public virtual async Task AddAsync(T entity)
        => await _collection.InsertOneAsync(entity);

    public virtual async Task UpdateAsync(T entity, FilterDefinition<T> filter)
        => await _collection.ReplaceOneAsync(WithTenant(filter), entity);

    public virtual async Task DeleteAsync(FilterDefinition<T> filter)
        => await _collection.DeleteOneAsync(WithTenant(filter));

    public virtual async Task DeleteAllAsync()
        => await _collection.DeleteManyAsync(TenantFilter);

    public virtual async Task<long> CountAsync()
        => await _collection.CountDocumentsAsync(TenantFilter);
}