using ERP.TenantService.Domain;
using ERP.TenantService.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace ERP.TenantService.Infrastructure.Persistence;

public class TenantDbContext : DbContext
{
    public TenantDbContext(DbContextOptions<TenantDbContext> options) : base(options) { }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<TenantSubscription> TenantSubscriptions => Set<TenantSubscription>();
    public DbSet<SubscriptionPlan> SubscriptionPlans => Set<SubscriptionPlan>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfiguration(new TenantConfiguration());
        modelBuilder.ApplyConfiguration(new TenantSubscriptionConfiguration());
        modelBuilder.ApplyConfiguration(new SubscriptionPlanConfiguration());
    }
}
