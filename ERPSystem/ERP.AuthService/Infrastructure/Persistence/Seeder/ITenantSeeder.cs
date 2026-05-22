namespace ERP.AuthService.Infrastructure.Persistence.Seeder;

public interface ITenantSeeder
{
    Task SeedTenantAsync(Guid tenantId, string slug);
}