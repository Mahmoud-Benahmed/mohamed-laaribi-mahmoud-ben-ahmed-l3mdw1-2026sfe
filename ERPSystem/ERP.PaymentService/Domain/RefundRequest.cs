namespace ERP.PaymentService.Domain;

public class RefundRequest
{
    public Guid Id { get; private set; }
    public Guid? TenantId { get; private set; }
    public Guid ClientId { get; private set; }
    public Guid InvoiceId { get; private set; }
    public RefundStatus Status { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public string? RefundReason { get; private set; }

    private readonly List<RefundLine> _lines = new();
    public IReadOnlyCollection<RefundLine> Lines => _lines;

    private RefundRequest() { }

    public RefundRequest(Guid clientId, Guid invoiceId, string? reason= null, Guid? tenantId = null)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        ClientId = clientId;
        InvoiceId= invoiceId;
        RefundReason = reason;
        Status = RefundStatus.PENDING;
    }

    public void AddLine(Guid paymentId, Guid allocationId,decimal amount)
    {
        if (Status != RefundStatus.PENDING)
            throw new InvalidOperationException("Cannot modify processed refund.");

        if (amount <= 0)
            throw new ArgumentException("Invalid amount.");

        if (_lines.Any(x => x.PaymentId == paymentId))
            throw new InvalidOperationException("Duplicate allocation.");

        _lines.Add(new RefundLine(paymentId, allocationId, Math.Round(amount, 2, MidpointRounding.AwayFromZero)));
    }

    public void Complete(string reason) 
    {
        if (Status == RefundStatus.COMPLETED)
            throw new InvalidOperationException("Cannot process a COMPLETED refund. This refund has been processed and sent to client.");
        Status = RefundStatus.COMPLETED;
        RefundReason = reason.Trim();
        CompletedAt = DateTime.UtcNow;
    }

}

public enum RefundStatus
{
    COMPLETED,
    PENDING
}