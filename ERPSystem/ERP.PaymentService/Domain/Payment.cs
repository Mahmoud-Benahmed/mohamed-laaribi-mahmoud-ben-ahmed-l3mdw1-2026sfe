using ERP.PaymentService.Application.Exceptions;
using ERP.PaymentService.Domain;
public class Payment
{
    public Guid Id { get; private set; }
    public Guid? TenantId { get; private set; }
    public string Number { get; private set; }
    public Guid ClientId { get; private set; }
    public decimal TotalAmount { get; private set; }
    public PaymentMethod Method { get; private set; }
    public PaymentStatus Status { get; private set; }
    public DateTime PaymentDate { get; private set; }
    public string? ExternalReference { get; private set; }
    public string? Notes { get; private set; }
    public DateTime? CancelledAt { get; private set; }

    private readonly List<PaymentInvoice> _allocations = new();
    public IReadOnlyCollection<PaymentInvoice> Allocations => _allocations;

    public decimal GetRemainingAmount() =>
        Math.Round(TotalAmount - _allocations.Sum(a => a.AmountAllocated), 2, MidpointRounding.AwayFromZero);

    private Payment() { }

    public Payment(
        string number, Guid clientId, decimal totalAmount,
        PaymentMethod method, DateTime paymentDate,
        string? externalReference = null,
        string? notes = null,
        Guid? tenantId= null)
    {
        Id = Guid.NewGuid();
        Number = number;
        ClientId = clientId;
        TotalAmount = Math.Round(totalAmount, 2, MidpointRounding.AwayFromZero); // ← round on creation
        TenantId = tenantId;
        Method = method;
        PaymentDate = paymentDate;
        ExternalReference = externalReference;
        Notes = notes;
        Status = PaymentStatus.DONE;
    }

    public void AllocateAmount(decimal amount, InvoiceCache cache)
    {
        amount = Math.Round(amount, 2, MidpointRounding.AwayFromZero); // ← round input first

        if (amount <= 0)
            throw new PaymentDomainException("Le montant affecté doit être positif.");

        var remaining = GetRemainingAmount();
        if (amount > remaining + 0.01m)  // ← tolerance for floating-point edge cases
            throw new PaymentDomainException(
                $"Le montant affecté ({amount:F2}) dépasse le restant du règlement ({remaining:F2}).");

        var invoiceRemaining = Math.Round(cache.TotalTTC - cache.PaidAmount, 2, MidpointRounding.AwayFromZero);
        if (amount > invoiceRemaining + 0.01m)  // ← same tolerance
            throw new PaymentDomainException(
                $"Le montant affecté ({amount:F2}) dépasse le restant de la facture ({invoiceRemaining:F2}).");

        _allocations.Add(new PaymentInvoice(Id, cache.Id, amount));
    }

    public void CorrectDetails(
        DateTime paymentDate,
        PaymentMethod method,
        string? externalReference,
        string? notes)
    {
        if (Status == PaymentStatus.CANCELLED)
            throw new PaymentAlreadyCancelledException(Id);

        PaymentDate = paymentDate;
        Method = method;
        ExternalReference = externalReference;
        Notes = notes;
    }

    public void Cancel()
    {
        if (Status == PaymentStatus.CANCELLED)
            throw new PaymentAlreadyCancelledException(Id);

        Status = PaymentStatus.CANCELLED;
        CancelledAt = DateTime.UtcNow;
    }
}

public enum PaymentMethod
{
    ESPECE,
    CHEQUE,
    VIREMENT,
    CARTE_BANCAIRE,
    MOBILE_PAYMENT,
    AUTRE
}

public enum PaymentStatus
{
    DONE,
    CANCELLED
}