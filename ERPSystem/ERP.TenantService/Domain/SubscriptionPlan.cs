namespace ERP.TenantService.Domain;

public class SubscriptionPlan
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Code { get; private set; } = string.Empty;
    public decimal MonthlyPrice { get; private set; }
    public decimal YearlyPrice { get; private set; }
    public int MaxUsers { get; private set; }
    public int MaxStorageMb { get; private set; }
    public bool IsActive { get; private set; } = true;

    private SubscriptionPlan() { }

    public static SubscriptionPlan Create(
        string name,
        string code,
        decimal monthlyPrice,
        decimal yearlyPrice,
        int maxUsers,
        int maxStorageMb)
    {
        return new SubscriptionPlan
        {
            Id = Guid.NewGuid(),
            Name = name,
            Code = code,
            MonthlyPrice = monthlyPrice,
            YearlyPrice = yearlyPrice,
            MaxUsers = maxUsers,
            MaxStorageMb = maxStorageMb,
            IsActive = true
        };
    }

    public void Update(
        string name,
        string code,
        decimal monthlyPrice,
        decimal yearlyPrice,
        int maxUsers,
        int maxStorageMb)
    {
        Name = name;
        Code = code;
        MonthlyPrice = monthlyPrice;
        YearlyPrice = yearlyPrice;
        MaxUsers = maxUsers;
        MaxStorageMb = maxStorageMb;
    }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;
}
