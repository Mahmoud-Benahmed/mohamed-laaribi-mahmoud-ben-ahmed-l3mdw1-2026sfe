using ERP.TenantService.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.TenantService.Infrastructure.Persistence.Configurations;

public class SubscriptionPlanConfiguration : IEntityTypeConfiguration<SubscriptionPlan>
{
    public void Configure(EntityTypeBuilder<SubscriptionPlan> builder)
    {
        builder.ToTable("SubscriptionPlans");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .ValueGeneratedNever();

        builder.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(p => p.Code)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(p => p.Code)
            .IsUnique();

        builder.Property(p => p.MonthlyPrice)
            .IsRequired()
            .HasColumnType("decimal(18,2)");

        builder.Property(p => p.YearlyPrice)
            .IsRequired()
            .HasColumnType("decimal(18,2)");

        builder.Property(p => p.MaxUsers)
            .IsRequired();

        builder.Property(p => p.MaxStorageMb)
            .IsRequired();

        builder.Property(p => p.IsActive)
            .HasDefaultValue(true);
    }
}
