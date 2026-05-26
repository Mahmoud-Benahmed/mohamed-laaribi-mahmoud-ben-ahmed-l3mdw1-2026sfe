using ERP.PaymentService.Application.DTO;

public class InvoiceCache
{
    public Guid Id { get; private set; }
    public Guid? TenantId { get; private set; }
    public string InvoiceNumber { get; private set; } = default!;
    public decimal TotalTTC { get; private set; }
    public Guid ClientId { get; private set; }
    public InvoiceStatus Status { get; private set; }
    public decimal PaidAmount { get; private set; }
    public decimal RemainingAmount => Math.Round(Math.Max(0, TotalTTC - PaidAmount), 2);
    public DateTimeOffset LastUpdated { get; private set; }

    private InvoiceCache() { }

    public static InvoiceCache From(InvoiceEventDto e) => new()
    {
        Id = e.Id,
        TenantId = e.TenantId,
        ClientId = e.ClientId,
        TotalTTC = Math.Round(e.TotalTTC, 2),  // ← round on creation
        InvoiceNumber = e.InvoiceNumber,
        PaidAmount = 0,
        Status = InvoiceStatus.UNPAID,
        LastUpdated = DateTimeOffset.UtcNow
    };

    public void ApplyPayment(decimal amount)
    {
        if (Status == InvoiceStatus.CANCELLED)
            throw new InvalidOperationException("Cannot apply payment to a cancelled invoice.");
        if (Status == InvoiceStatus.PAID)
            throw new InvalidOperationException("Cannot apply payment to an already paid invoice.");

        PaidAmount = Math.Round(PaidAmount + amount, 2);

        // Use a small tolerance to handle floating-point edge cases
        Status = PaidAmount >= TotalTTC - 0.01m
            ? InvoiceStatus.PAID
            : InvoiceStatus.UNPAID;

        LastUpdated = DateTimeOffset.UtcNow;
    }

    public void ReversePayment(decimal amount)
    {

        if (amount <= 0)
            throw new ArgumentException("Reversal amount must be positive.");

        if (amount > PaidAmount)
            throw new InvalidOperationException("Cannot reverse more than paid amount.");
        
        var newAmount = PaidAmount - amount;

        PaidAmount = Math.Round(
            Math.Max(0m, newAmount),
            2,
            MidpointRounding.AwayFromZero
        );

        Status = InvoiceStatus.UNPAID; // partially paid is still UNPAID

        LastUpdated = DateTimeOffset.UtcNow;  // ← was missing
    }

    public void MarkCancelled()
    {
        Status = InvoiceStatus.CANCELLED;
        LastUpdated = DateTimeOffset.UtcNow;
    }
}
public enum InvoiceStatus
{
    DRAFT,
    UNPAID,
    PAID,
    CANCELLED
}