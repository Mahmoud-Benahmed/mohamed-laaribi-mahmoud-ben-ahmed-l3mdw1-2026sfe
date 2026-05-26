using ERP.StockService.Domain;

public sealed class BonEntre : PieceStock
{
    public Guid FournisseurId { get; private set; }

    private readonly List<LigneEntre> _lignes = [];
    public IReadOnlyCollection<LigneEntre> Lignes => _lignes.AsReadOnly();

    private BonEntre() { }

    public static BonEntre Create(string numero, Guid fournisseurId, string? observation = null, Guid? tenantId = null)
    {
        if (string.IsNullOrWhiteSpace(numero))
            throw new ArgumentException("Numero is required.");

        if (fournisseurId == Guid.Empty)
            throw new ArgumentException("FournisseurId is required.");

        return new BonEntre
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Numero = numero.Trim(),
            FournisseurId = fournisseurId,
            Observation = observation?.Trim(),
            CreatedAt = DateTime.UtcNow,
        };
    }

    public void ClearLignes()
    {
        _lignes.Clear();
    }

    public LigneEntre AddLigne(Guid articleId, decimal qty, decimal price)
    {
        if (articleId == Guid.Empty)
            throw new ArgumentException("ArticleId is required.");

        if (qty <= 0)
            throw new ArgumentException("Quantity must be > 0.");

        if (price < 0)
            throw new ArgumentException("Price cannot be negative.");

        var existing = _lignes.FirstOrDefault(l => l.ArticleId == articleId);
        if (existing is not null)
        {
            existing.Update(existing.Quantity + qty, existing.Price);
            return existing;
        }
        ;

        LigneEntre ligne = LigneEntre.Create(Id, articleId, qty, price);
        _lignes.Add(ligne);

        return ligne;
    }
    public void Update(Guid fournisseurId, string? observation = null)
    {
        FournisseurId = fournisseurId;
        base.Update(observation);
    }

    public void RemoveLigne(Guid ligneId)
    {
        if (ligneId == Guid.Empty)
            throw new ArgumentException("LigneId is required.");

        if (!_lignes.Any())
            throw new InvalidOperationException("No lignes to remove.");

        LigneEntre ligne = _lignes.FirstOrDefault(l => l.Id == ligneId)
            ?? throw new InvalidOperationException("Ligne not found.");

        // 🔥 Business rule: cannot remove last ligne
        if (_lignes.Count == 1)
            throw new InvalidOperationException("Cannot remove the last ligne.");

        _lignes.Remove(ligne);

    }

    public void UpdateLigne(Guid ligneId, decimal qty, decimal price)
    {
        if (ligneId == Guid.Empty)
            throw new ArgumentException("LigneId is required.");

        if (qty <= 0)
            throw new ArgumentException("Quantity must be > 0.");

        if (price < 0)
            throw new ArgumentException("Price cannot be negative.");

        LigneEntre ligne = _lignes.FirstOrDefault(l => l.Id == ligneId)
            ?? throw new InvalidOperationException("Ligne not found.");

        ligne.Update(qty, price);

    }

    public override void ValidateLignes()
    {
        if (!_lignes.Any())
            throw new InvalidOperationException("BonEntre must have at least one ligne.");

        foreach (LigneEntre l in _lignes)
            l.Validate();
    }

    public decimal CalculateTotal() => _lignes.Sum(l => l.CalculateTotalLigne());
}