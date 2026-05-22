using ERP.AuthService.Infrastructure.Configuration;
using MongoDB.Bson;
using MongoDB.Driver;

namespace ERP.AuthService.Infrastructure.Persistence;

public static class MongoDbUserInitializer
{
    public static async Task EnsureAppUserCreatedAsync(MongoSettings settings)
    {
        var rootClient = new MongoClient(settings.RootConnectionString);
        var adminDb = rootClient.GetDatabase("admin");

        var filter = new BsonDocument("usersInfo", settings.AppUsername);

        var result = await adminDb.RunCommandAsync<BsonDocument>(filter);

        var users = result["users"].AsBsonArray;

        if (users.Count > 0)
            return;

        await adminDb.RunCommandAsync<BsonDocument>(new BsonDocument
        {
            { "createUser", settings.AppUsername },
            { "pwd",        settings.AppPassword },
            { "roles", new BsonArray
                {
                    new BsonDocument
                    {
                        { "role", "readWrite" },
                        { "db",   settings.DatabaseName }
                    }
                }
            }
        });
    }
}
