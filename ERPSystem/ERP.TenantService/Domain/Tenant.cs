namespace ERP.TenantService.Domain;

public class Tenant
{
    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public string Email { get; private set; }
    public string Phone { get; private set; }
    public string Slug { get; private set; }
    public string? LogoUrl { get; private set; }
    public string? PrimaryColor { get; private set; }
    public string? SecondaryColor { get; private set; }
    public string Currency { get; private set; }
    public string Locale { get; private set; }
    public string Timezone { get; private set; }
    public bool IsActive { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public void SetSubscription(TenantSubscription subscription)
        => Subscription = subscription;

    public TenantSubscription? Subscription { get; private set; }

    private Tenant() { }

    public static Tenant Create(
        string name,
        string email,
        string phone,
        string subdomainSlug,
        string? logoUrl = null,
        string? primaryColor = null,
        string? secondaryColor = null,
        string currency = "TND",
        string locale = "fr-TN",
        string timezone = "Africa/Tunisia"
    )
    {
        return new Tenant
        {
            Id = Guid.NewGuid(),
            Name = name,
            Email = email,
            Phone = phone,
            Slug = subdomainSlug,
            LogoUrl = logoUrl,
            PrimaryColor = primaryColor,
            SecondaryColor = secondaryColor,
            Currency = currency,
            Locale = locale,
            Timezone = timezone,
            IsActive = false,
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Update(
        string name,
        string email,
        string phone,
        string subdomainSlug,
        string? logoUrl,
        string? primaryColor,
        string? secondaryColor,
        string currency,
        string locale,
        string timezone)
    {
        Name = name;
        Email = email;
        Phone = phone;
        Slug = subdomainSlug;
        LogoUrl = logoUrl;
        PrimaryColor = primaryColor;
        SecondaryColor = secondaryColor;
        Currency = currency;
        Locale = locale;
        Timezone = timezone;
    }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;

    public void Delete()
    {
        IsActive = false;
        IsDeleted = true;
    }
    public void Restore() => IsDeleted = false;


    public void AssignSubscription(Guid subscriptionPlanId, DateTime startDate, DateTime endDate)
    {
        if (IsDeleted) throw new InvalidOperationException("Deleted tenant cannot receive a subscription.");
        //if (!IsActive) throw new InvalidOperationException("Tenant Deactivated, unable to assign a subscription to.");
        Subscription = TenantSubscription.Create(Id, subscriptionPlanId, startDate, endDate);
    }
}
