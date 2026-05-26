namespace ERP.PaymentService.Domain;
public class  PaymentInvoice
{
    public Guid Id { get; private set; }
    public Guid? TenantId { get; private set; }
    public Guid PaymentId { get; private set; }
    public Guid InvoiceId { get; private set; }   // plain value, no FK to InvoiceService
    public decimal AmountAllocated { get; private set; }

    public decimal RefundedAmount { get; private set; }

    public PaymentInvoice(Guid paymentId, Guid invoiceId, decimal amountAllocated, Guid? tenantId= null)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        PaymentId = paymentId;
        InvoiceId = invoiceId;
        AmountAllocated = Math.Round(amountAllocated, 2, MidpointRounding.AwayFromZero);
        RefundedAmount = 0;
    }

    public void Refund(decimal amount)
    {
        if (amount <= 0)
            throw new ArgumentException("Refund must be > 0");

        if (RefundedAmount + amount > AmountAllocated)
            throw new InvalidOperationException("Refund exceeds allocation");

        RefundedAmount += Math.Round(amount, 2, MidpointRounding.AwayFromZero);
    }
}