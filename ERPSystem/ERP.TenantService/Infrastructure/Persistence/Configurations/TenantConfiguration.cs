using ERP.TenantService.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.TenantService.Infrastructure.Persistence.Configurations;

public class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("Tenants");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id)
            .ValueGeneratedNever();

        builder.Property(t => t.Name)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(t => t.Email)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(t => t.Phone)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(t => t.SubdomainSlug)
            .IsRequired()
            .HasMaxLength(100);

        builder.HasIndex(t => t.SubdomainSlug)
            .IsUnique();

        builder.Property(t => t.LogoUrl)
            .HasMaxLength(500);

        builder.Property(t => t.PrimaryColor)
            .HasMaxLength(7);

        builder.Property(t => t.SecondaryColor)
            .HasMaxLength(7);

        builder.Property(t => t.Currency)
            .IsRequired()
            .HasMaxLength(10)
            .HasDefaultValue("TND");

        builder.Property(t => t.Locale)
            .IsRequired()
            .HasMaxLength(10)
            .HasDefaultValue("fr-TN");

        builder.Property(t => t.Timezone)
            .IsRequired()
            .HasMaxLength(50)
            .HasDefaultValue("Africa/Tunisia");

        builder.Property(t => t.IsActive)
            .HasDefaultValue(true);

        builder.Property(t => t.CreatedAt)
            .IsRequired();

        builder.HasOne(t => t.Subscription)
            .WithOne()
            .HasForeignKey<Domain.TenantSubscription>(s => s.TenantId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
