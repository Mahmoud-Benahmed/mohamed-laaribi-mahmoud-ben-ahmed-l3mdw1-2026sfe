using ERP.AuthService.Application.Services;
using MongoDB.Driver;

public class MongoDbContext
{
    private readonly ITenantContext _tenantContext;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IMongoDatabase _database;

    public MongoDbContext(string dbName, ITenantContext tenantContext, IMongoClient client)
    {
        _tenantContext = tenantContext;
        _database = client.GetDatabase(dbName);
        
    }

    public Guid? TenantId => _tenantContext.TenantId;  // ✅ delegates to ITenantContext
    public bool HasTenant => TenantId.HasValue;

    public IMongoCollection<T> Collection<T>(string name)
        => _database.GetCollection<T>(name);
}