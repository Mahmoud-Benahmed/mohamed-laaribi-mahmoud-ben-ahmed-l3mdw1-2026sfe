using ERP.AuthService.Application.Interfaces.Repositories;
using ERP.AuthService.Domain;
using ERP.AuthService.Properties;

namespace ERP.AuthService.Infrastructure.Persistence.Seeder;

public class TenantSeeder : ITenantSeeder
{
    private readonly IServiceScopeFactory _scopeFactory;

    public TenantSeeder(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task SeedTenantAsync(Guid tenantId, string slug)
    {
        using var scope = _scopeFactory.CreateScope();
        var services = scope.ServiceProvider;

        var context = services.GetRequiredService<MongoDbContext>();

        var roleRepo = services.GetRequiredService<IRoleRepository>();
        var userRepo = services.GetRequiredService<IAuthUserRepository>();
        var controleRepo = services.GetRequiredService<IControleRepository>();

        // Example: tenant default role
        var defaultRole = new Role(
            "ADMIN",
            tenantId
        );

        await roleRepo.AddAsync(defaultRole);

        // Example: tenant admin user
        var admin = new AuthUser
        (
            $"admin_{slug}",
            $"admin@{slug}-{AppProperties.AppDomain}",
            "Defualt Admin User",
            defaultRole.Id,
            tenantId
        );

        await userRepo.AddAsync(admin);
    }
}