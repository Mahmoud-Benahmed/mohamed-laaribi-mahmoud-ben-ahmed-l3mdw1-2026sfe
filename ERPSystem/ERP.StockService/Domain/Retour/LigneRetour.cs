public sealed class LigneRetour : LigneStock
{
    public Guid BonRetourId { get; private set; }
    public string? Remarque { get; private set; }
    public BonRetour? BonRetour { get; private set; }
    private LigneRetour() { }
    internal static LigneRetour Create(
        Guid bonRetourId, 
        Guid articleId, 
        decimal qty, 
        decimal price, 
        string? remarque = null, 
        Guid? tenantId = null
    )
    {
        LigneRetour l = new LigneRetour
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BonRetourId = bonRetourId,
            ArticleId = articleId,
            Quantity = qty,
            Price = price,
            Remarque = remarque?.Trim()
        };
        l.Validate();
        return l;
    }

    public void Update(decimal qty, decimal price, string? remarque)
    {
        Quantity = qty;
        Price = price;
        Remarque = remarque?.Trim();
        Validate();
    }

}