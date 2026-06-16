using ERP.StockService.Application.Services;
using ERP.StockService.Domain;
using ERP.StockService.Domain.LocalCache.Article;
using ERP.StockService.Domain.LocalCache.Client;
using ERP.StockService.Domain.LocalCache.Fournisseur;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.StockService.Infrastructure.Persistence;

public class StockDbContext : DbContext
{
    private readonly Guid? _tenantId;

    public StockDbContext(DbContextOptions<StockDbContext> options, ITenantContext? tenantContext = null)
        : base(options)
    {
        _tenantId = tenantContext?.TenantId;
    }

    public DbSet<BonEntre> BonEntres => Set<BonEntre>();
    public DbSet<BonSortie> BonSorties => Set<BonSortie>();
    public DbSet<BonRetour> BonRetours => Set<BonRetour>();
    public DbSet<LigneEntre> LigneEntres => Set<LigneEntre>();
    public DbSet<LigneSortie> LigneSorties => Set<LigneSortie>();
    public DbSet<LigneRetour> LigneRetours => Set<LigneRetour>();
    public DbSet<BonNumber> BonNumber => Set<BonNumber>();
    public DbSet<JournalStock> JournalStocks => Set<JournalStock>();
    public DbSet<ArticleCache> ArticleCaches => Set<ArticleCache>();
    public DbSet<ArticleCategoryCache> ArticleCategoryCaches => Set<ArticleCategoryCache>();
    public DbSet<InvoiceBonSortieMapping> InvoiceBonSortieMappings { get; set; }


    public DbSet<ClientCache> ClientCaches => Set<ClientCache>();
    public DbSet<Domain.LocalCache.Client.CategoryCache> ClientCategoryMasterCaches
    => Set<Domain.LocalCache.Client.CategoryCache>();
    public DbSet<ClientCategoryCache> ClientCategoryAssignments
    => Set<ClientCategoryCache>();
    public DbSet<FournisseurCache> FournisseurCaches => Set<FournisseurCache>();

    protected override void OnModelCreating(ModelBuilder m)
    {
        m.ApplyConfigurationsFromAssembly(typeof(StockDbContext).Assembly);

        m.Entity<BonNumber>().HasQueryFilter(b              => (b.TenantId == _tenantId || b.TenantId == null));
        m.Entity<BonEntre>().HasQueryFilter(b               => (b.TenantId == _tenantId || b.TenantId == null));
        m.Entity<BonSortie>().HasQueryFilter(b              => (b.TenantId == _tenantId || b.TenantId == null));
        m.Entity<BonRetour>().HasQueryFilter(b              => (b.TenantId == _tenantId || b.TenantId == null));
        
        m.Entity<LigneEntre>().HasQueryFilter(b             => (b.TenantId == _tenantId || b.TenantId == null));
        m.Entity<LigneSortie>().HasQueryFilter(b            => (b.TenantId == _tenantId || b.TenantId == null));
        m.Entity<LigneRetour>().HasQueryFilter(b            => (b.TenantId == _tenantId || b.TenantId == null));
        m.Entity<JournalStock>().HasQueryFilter(b           => (b.TenantId == _tenantId || b.TenantId == null));
        
        m.Entity<ArticleCache>().HasQueryFilter(b           => !b.IsDeleted && (b.TenantId == _tenantId || b.TenantId == null));
        m.Entity<ArticleCategoryCache>().HasQueryFilter(b   => !b.IsDeleted && (b.TenantId == _tenantId || b.TenantId == null));
        
        m.Entity<ClientCache>().HasQueryFilter(b            => !b.IsDeleted && (b.TenantId == _tenantId || b.TenantId == null)); 
        m.Entity<CategoryCache>()
            .HasQueryFilter(c => !c.IsDeleted && (_tenantId == null || c.TenantId == _tenantId));

        m.Entity<FournisseurCache>().HasQueryFilter(b       => !b.IsDeleted && (b.TenantId == _tenantId || b.TenantId == null));
    }
}

// ── BonNumber ─────────────────────────────────────────────────────────────────
internal class DocumentNumberSequenceConfiguration : IEntityTypeConfiguration<BonNumber>
{
    public void Configure(EntityTypeBuilder<BonNumber> b)
    {
        b.ToTable("BonNumbers");
        b.HasKey(s => s.Id);
        b.Property(s => s.Id).ValueGeneratedOnAdd();
        b.Property(s => s.DocumentType).IsRequired().HasMaxLength(50);
        b.Property(s => s.Prefix).IsRequired().HasMaxLength(10);
        b.Property(s => s.LastNumber).IsRequired();
        b.Property(s => s.Padding).IsRequired();
    }
}

// ── BonEntre ──────────────────────────────────────────────────────────────────
internal class BonEntreConfiguration : IEntityTypeConfiguration<BonEntre>
{
    public void Configure(EntityTypeBuilder<BonEntre> b)
    {
        b.ToTable("BonEntres");
        b.HasKey(x => x.Id);
        b.Property(x => x.Numero).IsRequired().HasMaxLength(50);
        b.Property(x => x.Observation).HasMaxLength(1000);
        b.Property(x => x.CreatedAt).IsRequired();
        b.Property(x => x.UpdatedAt).IsConcurrencyToken(false).ValueGeneratedNever();
        b.HasIndex(x => new {x.Numero, x.TenantId }).IsUnique().HasDatabaseName("IX_BonEntres_Numero");
        b.HasMany(x => x.Lignes)
         .WithOne(l => l.BonEntre)
         .HasForeignKey(l => l.BonEntreId)
         .OnDelete(DeleteBehavior.Cascade);
        b.Navigation(x => x.Lignes)
         .HasField("_lignes")
         .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

// ── LigneEntre ────────────────────────────────────────────────────────────────
internal class LigneEntreConfiguration : IEntityTypeConfiguration<LigneEntre>
{
    public void Configure(EntityTypeBuilder<LigneEntre> b)
    {
        b.ToTable("LigneEntres");
        b.HasKey(l => l.Id);
        b.Property(l => l.Id).ValueGeneratedNever();
        b.Property(l => l.ArticleId).IsRequired();
        b.Property(l => l.Quantity).IsRequired().HasPrecision(18, 4);
        b.Property(l => l.Price).IsRequired().HasPrecision(18, 4);
    }
}

// ── BonSortie ─────────────────────────────────────────────────────────────────
internal class BonSortieConfiguration : IEntityTypeConfiguration<BonSortie>
{
    public void Configure(EntityTypeBuilder<BonSortie> b)
    {
        b.ToTable("BonSorties");
        b.HasKey(x => x.Id);
        b.Property(x => x.Numero).IsRequired().HasMaxLength(50);
        b.Property(x => x.Observation).HasMaxLength(1000);
        b.Property(x => x.CreatedAt).IsRequired();
        b.Property(x => x.UpdatedAt).IsConcurrencyToken(false).ValueGeneratedNever();
        b.HasIndex(x => new {x.Numero, x.TenantId }).IsUnique().HasDatabaseName("IX_BonSorties_Numero");
        b.HasMany(x => x.Lignes)
         .WithOne(l => l.BonSortie)
         .HasForeignKey(l => l.BonSortieId)
         .OnDelete(DeleteBehavior.Cascade);
        b.Navigation(x => x.Lignes)
         .HasField("_lignes")
         .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

// ── LigneSortie ───────────────────────────────────────────────────────────────
internal class LigneSortieConfiguration : IEntityTypeConfiguration<LigneSortie>
{
    public void Configure(EntityTypeBuilder<LigneSortie> b)
    {
        b.ToTable("LigneSorties");
        b.HasKey(l => l.Id);
        b.Property(l => l.Id).ValueGeneratedNever();
        b.Property(l => l.ArticleId).IsRequired();
        b.Property(l => l.Quantity).IsRequired().HasPrecision(18, 4);
        b.Property(l => l.Price).IsRequired().HasPrecision(18, 4);
    }
}

// ── BonRetour ─────────────────────────────────────────────────────────────────
internal class BonRetourConfiguration : IEntityTypeConfiguration<BonRetour>
{
    public void Configure(EntityTypeBuilder<BonRetour> b)
    {
        b.ToTable("BonRetours");
        b.HasKey(x => x.Id);
        b.Property(x => x.Numero).IsRequired().HasMaxLength(50);
        b.Property(x => x.Motif).IsRequired().HasMaxLength(500);
        b.Property(x => x.Observation).HasMaxLength(1000);
        b.Property(x => x.CreatedAt).IsRequired();
        b.Property(x => x.UpdatedAt).IsConcurrencyToken(false).ValueGeneratedNever();
        b.Property(x => x.SourceType).IsRequired().HasConversion<string>().HasMaxLength(20);
        b.HasIndex(x => new {x.Numero, x.TenantId }).IsUnique().HasDatabaseName("IX_BonRetours_Numero");
        b.HasMany(x => x.Lignes)
         .WithOne(l => l.BonRetour)
         .HasForeignKey(l => l.BonRetourId)
         .OnDelete(DeleteBehavior.Cascade);
        b.Navigation(x => x.Lignes)
         .HasField("_lignes")
         .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

// ── LigneRetour ───────────────────────────────────────────────────────────────
internal class LigneRetourConfiguration : IEntityTypeConfiguration<LigneRetour>
{
    public void Configure(EntityTypeBuilder<LigneRetour> b)
    {
        b.ToTable("LigneRetours");
        b.HasKey(l => l.Id);
        b.Property(l => l.Id).ValueGeneratedNever();
        b.Property(l => l.ArticleId).IsRequired();
        b.Property(l => l.Quantity).IsRequired().HasPrecision(18, 4);
        b.Property(l => l.Price).IsRequired().HasPrecision(18, 4);
        b.Property(l => l.Remarque).HasMaxLength(500);
    }
}

// ── JournalStock ──────────────────────────────────────────────────────────────
internal class JournalStockConfiguration : IEntityTypeConfiguration<JournalStock>
{
    public void Configure(EntityTypeBuilder<JournalStock> b)
    {
        b.ToTable("JournalStocks");
        b.HasKey(x => x.Id);
        b.Property(x => x.Quantity).HasColumnType("decimal(18,3)");
        b.Property(x => x.StockBefore).HasColumnType("decimal(18,3)");
        b.Property(x => x.StockAfter).HasColumnType("decimal(18,3)");
        b.Property(x => x.SourceService).HasMaxLength(100);
        b.Property(x => x.SourceOperation).HasMaxLength(100);
        b.Property(x => x.MovementType).HasConversion<string>();
        b.HasIndex(x => x.ArticleId);
        b.HasIndex(x => x.CreatedAt);
        b.HasIndex(x => x.MovementType);
    }
}

// ── CategoryCache ─────────────────────────────────────────────────────────────
internal class ArtCategoryCacheConfiguration : IEntityTypeConfiguration<Domain.LocalCache.Article.ArticleCategoryCache>
{
    public void Configure(EntityTypeBuilder<Domain.LocalCache.Article.ArticleCategoryCache> b)
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

// ── ArticleCache ──────────────────────────────────────────────────────────────
internal class ArticleCacheConfiguration : IEntityTypeConfiguration<ArticleCache>
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
        b.Property(c => c.DuePaymentPeriod).IsRequired();
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
internal class FournisseurCacheConfiguration : IEntityTypeConfiguration<FournisseurCache>
{
    public void Configure(EntityTypeBuilder<FournisseurCache> entity)
    {
        // Primary key
        entity.HasKey(e => e.Id);

        // Indexes
        entity.HasIndex(e => new { e.Name, e.TenantId })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");

        entity.HasIndex(e => new { e.Email, e.TenantId })
            .HasFilter("[Email] IS NOT NULL AND [IsDeleted] = 0");
        entity.HasIndex(e => new { e.Phone, e.TenantId });

        // Property configurations
        entity.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(200);
        entity.Property(e => e.Address)
            .IsRequired()
            .HasMaxLength(500);
        entity.Property(e => e.Phone)
            .IsRequired()
            .HasMaxLength(20);
        entity.Property(e => e.Email)
            .HasMaxLength(200);
        entity.Property(f => f.TaxNumber)
            .IsRequired(false)   // ← was IsRequired()
            .HasMaxLength(50);

        entity.HasIndex(f => new { f.TaxNumber, f.TenantId })
            .IsUnique()
            .HasDatabaseName("IX_FournisseurCaches_TaxNumber")
            .HasFilter("[IsDeleted] = 0 AND [TaxNumber] IS NOT NULL");

        entity.Property(e => e.RIB)
            .IsRequired()
            .HasMaxLength(50);

        entity.Property(e => e.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("GETUTCDATE()");
    }
}

internal class InvoiceBonSortieMappingConfiguration : IEntityTypeConfiguration<InvoiceBonSortieMapping>
{
    public void Configure(EntityTypeBuilder<InvoiceBonSortieMapping> entity)
    {
        entity.HasKey(e => e.Id);
        entity.HasIndex(e => new { e.InvoiceId, e.TenantId }).IsUnique();
        entity.HasIndex(e => new { e.BonSortieId, e.TenantId });
    }
}