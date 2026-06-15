namespace ERP.StockService.Domain;

public class BonNumber
{
    public Guid Id { get; private set; }
    public Guid? TenantId { get; private set; }
    public string DocumentType { get; private set; }  // "BON_ENTRE", "BON_SORTIE", "BON_RETOUR"
    public string Prefix { get; private set; }
    public int LastNumber { get; private set; }
    public int Padding { get; private set; }

    // Required by EF Core
    private BonNumber() { }

    public BonNumber(string documentType, string prefix, int padding = 6, Guid? tenantId= null)
    {
        if (string.IsNullOrWhiteSpace(documentType))
            throw new ArgumentException("Document type cannot be empty.", nameof(documentType));
        if (string.IsNullOrWhiteSpace(prefix))
            throw new ArgumentException("Prefix cannot be empty.", nameof(prefix));
        if (padding <= 0)
            throw new ArgumentOutOfRangeException(nameof(padding), "Padding must be greater than zero.");

        Id = Guid.NewGuid();
        DocumentType = documentType.Trim().ToUpperInvariant();
        Prefix = prefix.Trim().ToUpperInvariant();
        Padding = padding;
        LastNumber = 0;
    }

    /// <summary>
    /// Increments the sequence number.
    /// Called only within a database transaction/lock.
    /// </summary>
    public void Increment() => LastNumber++;

    /// <summary>
    /// Formats the document number as: {Prefix}-{Year}-{LastNumber padded to Padding digits}
    /// e.g. Prefix="BE", Year=2026, LastNumber=42, Padding=6 → "BE-2026-000042"
    /// </summary>
    public string FormatNumber(int year) =>
        $"{Prefix}-{year}-{LastNumber.ToString().PadLeft(Padding, '0')}";
}