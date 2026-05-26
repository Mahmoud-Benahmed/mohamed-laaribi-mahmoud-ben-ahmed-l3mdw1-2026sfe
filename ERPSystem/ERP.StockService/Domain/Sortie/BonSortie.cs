
using ERP.StockService.Domain;

public sealed class BonSortie : PieceStock
{
    public Guid ClientId { get; private set; }
    private readonly List<LigneSortie> _lignes = [];
    public IReadOnlyCollection<LigneSortie> Lignes => _lignes.AsReadOnly();
    private BonSortie() { }

    public static BonSortie Create(string numero, Guid clientId, string? observation = null, Guid? tenantId = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Numero = numero.Trim(), 
            ClientId = clientId, 
            Observation = observation?.Trim(), 
            CreatedAt = DateTime.UtcNow };

    public void Update(Guid clientId, string? observation = null)
    {
        ClientId = clientId;
        base.Update(observation);
    }
    public LigneSortie AddLigne(Guid articleId, decimal qty, decimal price)
    {
        if (qty <= 0)
            throw new ArgumentException("Quantity must be > 0");

        if (price < 0)
            throw new ArgumentException("Price cannot be negative");

        LigneSortie l = LigneSortie.Create(Id, articleId, qty, price);
        _lignes.Add(l);
        return l;
    }

    public void ClearLignes()
    {
        _lignes.Clear();
    }

    public override void ValidateLignes()
    {
        if (!_lignes.Any()) throw new InvalidOperationException("BonSortie must have at least one ligne.");
        foreach (LigneSortie l in _lignes) l.Validate();
    }

    public decimal CalculateTotal() => _lignes.Sum(l => l.CalculateTotalLigne());
}