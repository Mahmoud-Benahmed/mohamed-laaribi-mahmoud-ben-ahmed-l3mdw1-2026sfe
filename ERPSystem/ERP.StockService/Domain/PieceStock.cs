namespace ERP.StockService.Domain;

public abstract class PieceStock
{
    public Guid Id { get; protected set; }
    public Guid? TenantId { get; protected set; }
    public string Numero { get; init; } = default!;
    public string? Observation { get; protected set; }
    public DateTime CreatedAt { get; protected set; }
    public DateTime? UpdatedAt { get; protected set; }

    public abstract void ValidateLignes();

    public void Update(string? observation = null)
    {
        Observation = observation?.Trim();
        UpdatedAt = DateTime.UtcNow;
    }


}