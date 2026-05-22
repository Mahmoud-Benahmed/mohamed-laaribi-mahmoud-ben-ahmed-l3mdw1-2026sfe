using ERP.AuthService.Application.Services;
using MongoDB.Driver;

namespace ERP.AuthService.Infrastructure.Persistence;

public class MongoDbContext
{
    private readonly IMongoDatabase _database;
    public Guid? TenantId { get; }
    public bool HasTenant => TenantId.HasValue;

    public MongoDbContext(string connectionString, string dbName, ITenantContext? tenantContext = null)
    {
        var client = new MongoClient(connectionString);
        _database = client.GetDatabase(dbName);

        TenantId = tenantContext?.TenantId;
    }

    public IMongoCollection<T> Collection<T>(string name)
        => _database.GetCollection<T>(name);

    public Task DropCollectionAsync(string name)
        => _database.DropCollectionAsync(name);

    public Task DropDatabaseAsync()
        => _database.Client.DropDatabaseAsync(_database.DatabaseNamespace.DatabaseName);
}