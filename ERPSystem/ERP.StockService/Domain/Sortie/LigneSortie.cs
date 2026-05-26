public sealed class LigneSortie : LigneStock
{
    public Guid BonSortieId { get; private set; }
    public BonSortie? BonSortie { get; private set; }
    private LigneSortie() { }
    internal static LigneSortie Create(Guid bonSortieId, Guid articleId, decimal qty, decimal price, Guid? tenantId = null)
    {
        LigneSortie l = new LigneSortie
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BonSortieId = bonSortieId,
            ArticleId = articleId,
            Quantity = qty,
            Price = price
        };
        l.Validate();
        return l;
    }
}