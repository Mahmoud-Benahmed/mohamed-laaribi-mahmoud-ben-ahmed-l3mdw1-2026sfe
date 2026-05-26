
public abstract class LigneStock
{
    public Guid Id { get; protected set; }
    public Guid? TenantId { get; protected set; }
    public Guid ArticleId { get; protected set; }
    public decimal Quantity { get; protected set; }
    public decimal Price { get; protected set; }

    public virtual decimal CalculateTotalLigne() => Quantity * Price;

    public virtual void Validate()
    {
        if (Quantity <= 0)
            throw new ArgumentException("Quantity must be greater than zero.");
        if (Price < 0)
            throw new ArgumentException("Price cannot be negative.");
    }

    public virtual void Update(decimal qty, decimal price)
    {
        Quantity = qty;
        Price = price;
        Validate();
    }

}