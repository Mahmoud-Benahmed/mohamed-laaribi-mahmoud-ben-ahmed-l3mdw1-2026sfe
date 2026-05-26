using ERP.PaymentService.Application.Services;
using ERP.PaymentService.Domain;
using ERP.PaymentService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;


public class PaymentDbContext : DbContext
{
    private readonly Guid? _tenantId;
    public PaymentDbContext(DbContextOptions<PaymentDbContext> options, ITenantContext? tenantContext = null)
    : base(options) 
    { 
        _tenantId = tenantContext?.TenantId;
    }

    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<PaymentInvoice> PaymentsInvoices => Set<PaymentInvoice>();
    public DbSet<InvoiceCache> InvoiceCaches => Set<InvoiceCache>();
    public DbSet<PaymentSequence> PaymentSequences=> Set<PaymentSequence>();
    public DbSet<RefundRequest> Refunds=> Set<RefundRequest>();

    protected override void OnModelCreating(ModelBuilder m)
    {
        m.ApplyConfigurationsFromAssembly(typeof(PaymentDbContext).Assembly);
        m.Entity<Payment>()
            .HasQueryFilter(p => p.TenantId == _tenantId);
        m.Entity<PaymentInvoice>()
            .HasQueryFilter(pi => pi.TenantId == _tenantId);
        m.Entity<PaymentSequence>()
            .HasQueryFilter(p => p.TenantId == _tenantId);
        m.Entity<RefundRequest>()
            .HasQueryFilter(p => p.TenantId == _tenantId);
        m.Entity<InvoiceCache>()
            .HasQueryFilter(i => i.TenantId == _tenantId);
    }
}

internal class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("Payments");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Number)
               .IsRequired()
               .HasMaxLength(50);

        builder.Property(p => p.ClientId)
               .IsRequired();

        builder.Property(p => p.TotalAmount)
               .IsRequired()
               .HasPrecision(18, 2);

        builder.Property(p => p.Method)
               .IsRequired()
               .HasConversion<string>()
               .HasMaxLength(20);

        builder.Property(p => p.Status)
               .IsRequired()
               .HasConversion<string>()
               .HasMaxLength(20);

        builder.Property(p => p.PaymentDate)
               .IsRequired();

        builder.Property(p => p.ExternalReference)
               .HasMaxLength(100);  // nullable — no IsRequired()

        builder.Property(p => p.Notes)
               .HasMaxLength(500);  // nullable — no IsRequired()

        // ✅ CancelledAt should be nullable — a payment isn't always cancelled
        builder.Property(p => p.CancelledAt)
               .IsRequired(false);

        builder.HasIndex(p => p.ClientId);
        builder.HasIndex(p => new {p.TenantId, p.Number}).IsUnique();

        builder.Navigation(p => p.Allocations)
               .HasField("_allocations")
               .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(p => p.Allocations)
               .WithOne()
               .HasForeignKey(a => a.PaymentId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}

internal class PaymentInvoiceConfiguration : IEntityTypeConfiguration<PaymentInvoice>
{
    public void Configure(EntityTypeBuilder<PaymentInvoice> builder)
    {
        builder.ToTable("PaymentInvoices");
        builder.HasKey(pi => pi.Id);

        builder.Property(pi => pi.AmountAllocated).HasPrecision(18, 2);
        builder.Property(pi => pi.RefundedAmount).HasPrecision(18, 2);

        builder.Property(pi => pi.InvoiceId).IsRequired();
        builder.Property(pi => pi.PaymentId).IsRequired();

        builder.HasIndex(pi => pi.InvoiceId);
        builder.HasIndex(pi => pi.PaymentId);

        // one allocation per invoice per payment
        builder.HasIndex(pi => new { pi.PaymentId, pi.InvoiceId, pi.TenantId })
               .IsUnique();
    }
}

internal class InvoiceCacheConfiguration : IEntityTypeConfiguration<InvoiceCache>
{
    public void Configure(EntityTypeBuilder<InvoiceCache> builder)
    {
        builder.ToTable("InvoiceCache");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.InvoiceNumber)
               .IsRequired()
               .HasMaxLength(50);

        builder.Property(c => c.TotalTTC)
               .IsRequired()
               .HasPrecision(18, 2);

        builder.Property(c => c.PaidAmount)
               .IsRequired()
               .HasPrecision(18, 2);

        builder.Property(c => c.ClientId)
               .IsRequired();

        builder.Property(c => c.Status)
               .IsRequired()
               .HasConversion<string>()
               .HasMaxLength(20);

        builder.Property(c => c.LastUpdated)
               .IsRequired();
        
        builder.Ignore(c => c.RemainingAmount);

        // speeds up lookups when validating allocations by client
        builder.HasIndex(c => c.ClientId);
    }
}

internal class PaymentSequenceConfiguration : IEntityTypeConfiguration<PaymentSequence>
{
    public void Configure(EntityTypeBuilder<PaymentSequence> builder)
    {
        builder.ToTable("PaymentSequences");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Year).IsRequired();
        builder.Property(s => s.CurrentNumber).IsRequired();
        builder.Property(s => s.CreatedAt).IsRequired();
        builder.Property(s => s.UpdatedAt).IsRequired();

        // one sequence row per year — no duplicates
        builder.HasIndex(s => new { s.Year, s.TenantId }).IsUnique();
    }
}

internal class RefundRequestConfiguration : IEntityTypeConfiguration<RefundRequest>
{
    public void Configure(EntityTypeBuilder<RefundRequest> builder)
    {
        builder.ToTable("Refunds");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.ClientId).IsRequired();
        builder.Property(e => e.InvoiceId).IsRequired();
        builder.HasIndex(e => new { e.InvoiceId, e.TenantId }).IsUnique();

        builder.Property(e => e.Status)
               .HasConversion<string>()
               .HasMaxLength(50)
               .IsRequired();

        builder.Property(e => e.CompletedAt).IsRequired(false);

        builder.Property(e => e.RefundReason)
               .HasMaxLength(500);


        // 🔥 THIS IS THE IMPORTANT PART
        builder.OwnsMany(e => e.Lines, lines =>
        {
            lines.ToTable("RefundLines");

            lines.WithOwner().HasForeignKey(l => l.RefundRequestId);
            lines.HasKey(l => new { l.RefundRequestId, l.PaymentAllocationId });

            lines.Property(l => l.PaymentId).IsRequired();
            lines.Property(l => l.PaymentAllocationId).IsRequired();

            lines.Property(l => l.Amount)
                 .HasPrecision(18, 2)
                 .IsRequired();

            lines.HasIndex(l => l.PaymentAllocationId);
        });
    }
}