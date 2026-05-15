namespace ERP.TenantService.Domain;

public class Tenant
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string Phone { get; private set; } = string.Empty;
    public string SubdomainSlug { get; private set; } = string.Empty;
    public string? LogoUrl { get; private set; }
    public string? PrimaryColor { get; private set; }
    public string? SecondaryColor { get; private set; }
    public string Currency { get; private set; } = "TND";
    public string Locale { get; private set; } = "fr-TN";
    public string Timezone { get; private set; } = "Africa/Tunisia";
    public bool IsActive { get; private set; } = true;
    public DateTime CreatedAt { get; private set; }

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
        string timezone = "Africa/Tunisia")
    {
        return new Tenant
        {
            Id = Guid.NewGuid(),
            Name = name,
            Email = email,
            Phone = phone,
            SubdomainSlug = subdomainSlug,
            LogoUrl = logoUrl,
            PrimaryColor = primaryColor,
            SecondaryColor = secondaryColor,
            Currency = currency,
            Locale = locale,
            Timezone = timezone,
            IsActive = true,
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
        SubdomainSlug = subdomainSlug;
        LogoUrl = logoUrl;
        PrimaryColor = primaryColor;
        SecondaryColor = secondaryColor;
        Currency = currency;
        Locale = locale;
        Timezone = timezone;
    }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;

    public void AssignSubscription(Guid subscriptionPlanId, DateTime startDate, DateTime endDate)
    {
        Subscription = TenantSubscription.Create(Id, subscriptionPlanId, startDate, endDate);
    }
}
