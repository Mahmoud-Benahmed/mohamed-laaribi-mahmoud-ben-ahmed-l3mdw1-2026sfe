using ERP.TenantService.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.TenantService.Infrastructure.Persistence.Configurations;

public class TenantSubscriptionConfiguration : IEntityTypeConfiguration<TenantSubscription>
{
    public void Configure(EntityTypeBuilder<TenantSubscription> builder)
    {
        builder.ToTable("TenantSubscriptions");

        builder.HasKey(s => s.TenantId);

        builder.Property(s => s.TenantId)
            .ValueGeneratedNever();

        builder.Property(s => s.SubscriptionPlanId)
            .IsRequired();

        builder.Property(s => s.StartDate)
            .IsRequired();

        builder.Property(s => s.EndDate)
            .IsRequired();

        builder.HasOne(s => s.Plan)
            .WithMany()
            .HasForeignKey(s => s.SubscriptionPlanId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
