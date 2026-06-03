using ERP.InvoiceService.Application.Services;
using ERP.InvoiceService.Domain.LocalCache.Article;
using ERP.InvoiceService.Domain.LocalCache.Client;
using ERP.InvoiceService.Domain.LocalCache.Tenant;
using InvoiceService.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.InvoiceService.Infrastructure.Persistence;
public class InvoiceDbContext : DbContext
{
    private readonly Guid? _tenantId;
    public InvoiceDbContext(DbContextOptions<InvoiceDbContext> options, ITenantContext? tenantContext = null): base(options)
    {
        _tenantId = tenantContext?.TenantId;
    }

    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceItem> InvoiceItems => Set<InvoiceItem>();
    public DbSet<InvoiceSequence> InvoiceSequences { get; set; }

    public DbSet<ArticleCache> ArticleCaches => Set<ArticleCache>();
    public DbSet<ArticleCategoryCache> ArticleCategoryCaches => Set<ArticleCategoryCache>();

    public DbSet<ClientCache> ClientCaches => Set<ClientCache>();
    public DbSet<CategoryCache> ClientCategoryMasterCaches => Set<CategoryCache>();
    public DbSet<ClientCategoryCache> ClientCategoryAssignments => Set<ClientCategoryCache>();
    public DbSet<TenantCache> TenantCaches => Set<TenantCache>();

    protected override void OnModelCreating(ModelBuilder m)
    {
        m.ApplyConfigurationsFromAssembly(typeof(InvoiceDbContext).Assembly);
        m.Entity<Invoice>()
            .HasQueryFilter(i =>
                !i.IsDeleted &&
                (_tenantId == null || i.TenantId == _tenantId));
        m.Entity<InvoiceSequence>()
            .HasQueryFilter(i =>
                (_tenantId == null || i.TenantId == _tenantId));

        m.Entity<ArticleCategoryCache>()
            .HasQueryFilter(a => !a.IsDeleted && (_tenantId == null || a.TenantId == _tenantId));
        m.Entity<ArticleCache>()
            .HasQueryFilter(a => !a.IsDeleted && (_tenantId == null || a.TenantId == _tenantId));

        m.Entity<ClientCache>()
            .HasQueryFilter(c => !c.IsDeleted && (_tenantId == null || c.TenantId == _tenantId));
        m.Entity<CategoryCache>()
            .HasQueryFilter(c => !c.IsDeleted && (_tenantId == null || c.TenantId == _tenantId));

        m.Entity<InvoiceSequence>()
            .HasQueryFilter(c => _tenantId == null || c.TenantId == _tenantId);
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// INVOICE CONFIGURATIONS
// ═══════════════════════════════════════════════════════════════════════════

internal sealed class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> entity)
    {
        entity.ToTable("Invoices");

        entity.HasKey(i => i.Id);

        entity.Property(i => i.InvoiceNumber)
                .IsRequired()
                .HasMaxLength(50);

        entity.Property(i => i.ClientFullName)
                .IsRequired()
                .HasMaxLength(200);

        entity.Property(i => i.ClientAddress)
                .IsRequired()
                .HasMaxLength(500);

        entity.Property(i => i.AdditionalNotes)
                .HasMaxLength(1000);

        entity.Property(x => x.DiscountRate)
            .HasPrecision(5, 2);

        entity.Property(x => x.TotalHT)
            .HasPrecision(18, 3);

        entity.Property(x => x.TotalTVA)
            .HasPrecision(18, 3);

        entity.Property(x => x.TotalTTC)
            .HasPrecision(18, 3);

        entity.Property(i => i.TaxCalculationMode)
            .HasConversion<string>()
            .HasMaxLength(20);

        entity.Property(i => i.Status)
                .HasConversion<string>()
                .HasMaxLength(20);

        entity.HasIndex(i => new { i.TenantId, i.InvoiceNumber }).IsUnique();

        entity.HasMany(i => i.Items)
                .WithOne()
                .HasForeignKey(ii => ii.InvoiceId)
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();
    }
}

internal sealed class InvoiceItemConfiguration : IEntityTypeConfiguration<InvoiceItem>
{
    public void Configure(EntityTypeBuilder<InvoiceItem> entity)
    {
        entity.ToTable("InvoiceItems");

        entity.HasKey(ii => ii.Id);

        entity.Property(ii => ii.ArticleName)
                .IsRequired()
                .HasMaxLength(200);

        entity.Property(ii => ii.Quantity)
                .HasPrecision(18, 3)
                .IsRequired();

        entity.Property(ii => ii.ArticleBarCode)
                .IsRequired()
                .HasMaxLength(100);

        entity.Property(ii => ii.UniPriceHT)
            .HasPrecision(18, 3);

        entity.Property(ii => ii.TotalHT)
            .HasPrecision(18, 3);

        entity.Property(ii => ii.TotalTTC)
            .HasPrecision(18, 3);

        entity.Property(ii => ii.TaxRate)
            .HasPrecision(5, 3);

        entity.Ignore(ii => ii.EffectivePriceHT);

        entity.Property<Guid>("InvoiceId").IsRequired();
    }
}

internal sealed class InvoiceSequenceConfiguration : IEntityTypeConfiguration<InvoiceSequence>
{
    public void Configure(EntityTypeBuilder<InvoiceSequence> entity)
    {
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Year).IsRequired();
        entity.Property(e => e.CurrentNumber).IsRequired();

        entity.HasIndex(e => new { e.TenantId, e.Year }).IsUnique();
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// CACHE CONFIGURATIONS
// ═══════════════════════════════════════════════════════════════════════════

internal sealed class ArtCategoryCacheConfiguration : IEntityTypeConfiguration<ArticleCategoryCache>
{
    public void Configure(EntityTypeBuilder<ArticleCategoryCache> b)
    {
        b.ToTable("ArticleCategoryCache");
        b.HasKey(c => c.Id);
        b.Property(c => c.Id).ValueGeneratedNever();
        b.Property(c => c.TenantId).IsRequired(false);
        b.Property(c => c.Name).IsRequired().HasMaxLength(100);
        b.Property(c => c.TVA).HasPrecision(5, 2);
        b.Property(c => c.IsDeleted).HasDefaultValue(false);
        b.Property(c => c.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
        b.Property(c => c.UpdatedAt).IsRequired(false);

        b.HasIndex(c => new { c.TenantId, c.Name })
                    .IsUnique()
                    .HasFilter("[IsDeleted] = 0");
    }
}

internal sealed class ArticleCacheConfiguration : IEntityTypeConfiguration<ArticleCache>
{
    public void Configure(EntityTypeBuilder<ArticleCache> b)
    {
        b.ToTable("ArticleCache");
        b.HasKey(a => a.Id);
        b.Property(a => a.Id).ValueGeneratedNever();
        b.Property(a => a.CodeRef).IsRequired().HasMaxLength(50);
        b.Property(a => a.BarCode).IsRequired().HasMaxLength(13);
        b.Property(a => a.Libelle).IsRequired().HasMaxLength(200);
        b.Property(a => a.Prix).HasPrecision(18, 3);
        b.Property(a => a.TVA).HasPrecision(5, 2);
        b.Property(a => a.Unit).IsRequired().HasMaxLength(50);
        b.Property(a => a.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
        b.Property(a => a.UpdatedAt).IsRequired(false);

        // FK to CategoryCache
        b.Property(a => a.CategoryId).IsRequired();
        b.HasOne(a => a.Category)
            .WithMany()
            .HasForeignKey(a => a.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        // Unique on active rows only
        b.HasIndex(a => new { a.TenantId, a.CodeRef })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");

        b.HasIndex(a => new { a.TenantId, a.BarCode })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");

        b.HasIndex(a => new { a.TenantId, a.IsDeleted });
        b.HasIndex(a => new { a.TenantId, a.CategoryId });
    }
}

internal class ClientCacheConfiguration : IEntityTypeConfiguration<ClientCache>
{
    public void Configure(EntityTypeBuilder<ClientCache> b)
    {
        b.ToTable("ClientCache");
        b.HasKey(c => c.Id);

        b.Property(c => c.Name).IsRequired().HasMaxLength(200);
        b.Property(c => c.Email).IsRequired().HasMaxLength(200);
        b.Property(c => c.Address).IsRequired().HasMaxLength(500);
        b.Property(c => c.Phone).HasMaxLength(20);
        b.Property(c => c.TaxNumber).HasMaxLength(50);
        b.Property(c => c.CreditLimit).HasPrecision(18, 3);
        b.Property(c => c.DelaiRetour);        // nullable int — no IsRequired
        b.Property(c => c.DuePaymentPeriod);   // nullable int — no IsRequired
        b.Property(c => c.IsBlocked).IsRequired();
        b.Property(c => c.IsDeleted).IsRequired();
        b.Property(c => c.CreatedAt).IsRequired();

        b.HasIndex(c => new { c.TenantId, c.Email })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");

        b.HasIndex(c => new { c.TenantId, c.IsBlocked });
    }
}

internal class CltCategoryCacheConfiguration : IEntityTypeConfiguration<Domain.LocalCache.Client.CategoryCache>
{
    public void Configure(EntityTypeBuilder<Domain.LocalCache.Client.CategoryCache> b)
    {
        b.ToTable("ClientCategoryCache");
        b.HasKey(c => c.Id);

        b.Property(c => c.Name).IsRequired().HasMaxLength(200);
        b.Property(c => c.Code).IsRequired().HasMaxLength(50);
        b.Property(c => c.DelaiRetour).IsRequired();
        b.Property(c => c.DuePaymentPeriod).IsRequired();
        b.Property(c => c.DiscountRate).HasPrecision(5, 3);
        b.Property(c => c.CreditLimitMultiplier).HasPrecision(8, 3);
        b.Property(c => c.UseBulkPricing).IsRequired();
        b.Property(c => c.IsActive).IsRequired();
        b.Property(c => c.IsDeleted).IsRequired();
        b.Property(c => c.CreatedAt).IsRequired();

        b.HasIndex(c => new { c.TenantId, c.Code })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");

        b.HasIndex(c => new { c.TenantId, c.IsActive });
    }
}

internal class ClientCategoryConfiguration : IEntityTypeConfiguration<Domain.LocalCache.Client.ClientCategoryCache>
{
    public void Configure(EntityTypeBuilder<Domain.LocalCache.Client.ClientCategoryCache> b)
    {
        b.ToTable("ClientCategoriesCache");

        b.HasKey(cc => new { cc.ClientId, cc.CategoryId });
        b.Property(cc => cc.AssignedAt).IsRequired();

        b.HasOne(cc => cc.Client)
            .WithMany(c => c.ClientCategories)
            .HasForeignKey(cc => cc.ClientId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(cc => cc.Category)
            .WithMany()
            .HasForeignKey(cc => cc.CategoryId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);
    }
}


internal sealed class TenantCacheConfiguration : IEntityTypeConfiguration<TenantCache>
{
public void Configure(EntityTypeBuilder<TenantCache> b)
{
    b.ToTable("TenantCaches");
    b.HasKey(t => t.TenantId);
    b.Property(t => t.TenantId).ValueGeneratedNever();
    b.Property(t => t.Slug).IsRequired().HasMaxLength(100);
    b.Property(t => t.Name).IsRequired().HasMaxLength(150);
    b.Property(t => t.Address).IsRequired().HasMaxLength(500);
    b.Property(t => t.Email).IsRequired().HasMaxLength(200);
    b.Property(t => t.Phone).IsRequired().HasMaxLength(20);
    b.Property(t => t.Currency).IsRequired().HasMaxLength(10);
    b.Property(t => t.LogoUrl).HasMaxLength(500);
    b.HasIndex(t => new { t.TenantId, t.Slug }).IsUnique();
    b.Property(t => t.PrimaryColor).HasMaxLength(7);
    b.Property(t => t.SecondaryColor).HasMaxLength(7);
}
}