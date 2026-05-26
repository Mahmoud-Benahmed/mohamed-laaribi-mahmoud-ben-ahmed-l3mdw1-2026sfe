namespace InvoiceService.Domain;

public class InvoiceSequence
{
    public Guid Id { get; private set; }
    public Guid? TenantId { get; private set; }
    public int Year { get; private set; }
    public int CurrentNumber { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private InvoiceSequence() { }

    public InvoiceSequence(int year, Guid? tenantId)
    {
        Id = Guid.NewGuid();
        Year = year;
        CurrentNumber = 0;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
        TenantId = tenantId;
    }

    public int GetNextNumber()
    {
        CurrentNumber++;
        UpdatedAt = DateTime.UtcNow;
        return CurrentNumber;
    }

    public string FormatInvoiceNumber()
    {
        return $"INV-{Year}-{CurrentNumber:D4}";
    }
}