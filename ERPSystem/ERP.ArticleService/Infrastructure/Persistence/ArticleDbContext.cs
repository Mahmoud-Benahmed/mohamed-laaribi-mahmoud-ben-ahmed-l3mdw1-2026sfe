using ERP.ArticleService.Application.Services;
using ERP.ArticleService.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.ArticleService.Infrastructure.Persistence
{
    public class ArticleDbContext : DbContext
    {
        private readonly Guid? _tenantId;

        public ArticleDbContext(
            DbContextOptions<ArticleDbContext> options,
            ITenantContext? tenantContext= null)  // ✅ inject tenant context
            : base(options)
        {
            _tenantId = tenantContext?.TenantId;
        }

        public DbSet<Article> Articles { get; set; }
        public DbSet<ArticleCode> ArticleCodes { get; set; }
        public DbSet<Category> Categories { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Always apply soft-delete + tenant filter together
            modelBuilder.Entity<Article>()
                .HasQueryFilter(a =>
                    !a.IsDeleted &&
                    (_tenantId == null || a.TenantId == _tenantId));

            modelBuilder.Entity<Category>()
                .HasQueryFilter(c =>
                    !c.IsDeleted &&
                    (_tenantId == null || c.TenantId == _tenantId));

            modelBuilder.Entity<ArticleCode>()
                .HasQueryFilter(c =>
                    (_tenantId == null || c.TenantId == _tenantId));

            base.OnModelCreating(modelBuilder);

            // Configure Article
            modelBuilder.Entity<Article>(entity =>
            {
                entity.ToTable("Articles");

                entity.HasKey(a => a.Id);

                entity.Property(a => a.Id)
                      .ValueGeneratedNever();

                entity.Property(a => a.CodeRef)
                      .IsRequired()
                      .HasMaxLength(50);

                entity.Property(a => a.Libelle)
                      .IsRequired()
                      .HasMaxLength(250);

                entity.Property(a => a.Prix)
                      .HasColumnType("decimal(18,2)");

                entity.Property(a => a.Unit)
                      .HasConversion<string>()
                      .HasMaxLength(20);

                entity.Property(a => a.IsDeleted)
                      .HasDefaultValue(false);

                entity.Property(a => a.TVA)
                      .HasColumnType("int");

                entity.Property(a => a.BarCode)
                      .HasMaxLength(13);

                entity.Property(a => a.CreatedAt)
                      .HasDefaultValueSql("GETUTCDATE()");

                entity.Property(a => a.UpdatedAt)
                      .IsRequired(false);

                // Relationship to Category
                entity.HasOne(a => a.Category)
                      .WithMany()
                      .HasForeignKey(a => a.CategoryId)
                      .OnDelete(DeleteBehavior.Restrict);

                // Unique indexes (active articles only)
                entity.HasIndex(a => new { a.TenantId, a.CodeRef }).IsUnique();

                entity.HasIndex(a => a.BarCode)
                      .IsUnique()
                      .HasFilter("[IsDeleted] = 0 AND [BarCode] IS NOT NULL");
            });

            // Configure ArticleCode
            modelBuilder.Entity<ArticleCode>(entity =>
            {
                entity.ToTable("ArticleCodes");

                entity.HasKey(c => c.Id);

                entity.Property(c => c.Id)
                      .ValueGeneratedNever();

                entity.Property(c => c.Prefix)
                      .HasMaxLength(10)
                      .IsRequired();

                entity.Property(c => c.LastNumber)
                      .HasDefaultValue(0);

                entity.Property(c => c.Padding)
                      .HasDefaultValue(6);

                // Unique index on Prefix
                entity.HasIndex(a => new { a.TenantId, a.Prefix }).IsUnique();
            });

            // Configure Category
            modelBuilder.Entity<Category>(entity =>
            {
                entity.ToTable("Categories");

                entity.HasKey(c => c.Id);

                entity.Property(c => c.Id)
                      .ValueGeneratedNever();

                entity.Property(c => c.Name)
                      .IsRequired()
                      .HasMaxLength(100);

                entity.Property(a => a.TVA)
                      .HasColumnType("int");

                entity.Property(c => c.IsDeleted)
                      .HasDefaultValue(false);

                entity.Property(c => c.CreatedAt)
                      .HasDefaultValueSql("GETUTCDATE()");

                entity.Property(c => c.UpdatedAt)
                      .IsRequired(false);


                entity.HasIndex(a => new { a.TenantId, a.Name })
                      .IsUnique()
                      .HasFilter("[IsDeleted] = 0");
            });
        }
    }
}