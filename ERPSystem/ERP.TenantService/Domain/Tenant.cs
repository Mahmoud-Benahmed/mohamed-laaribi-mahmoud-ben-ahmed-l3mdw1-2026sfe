namespace ERP.TenantService.Domain;

public class Tenant
{
    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public string Email { get; private set; }
    public string Phone { get; private set; }
    public string Slug { get; private set; }
    public string Address { get; private set; }
    public string? LogoUrl { get; private set; }
    public string? PrimaryColor { get; private set; }
    public string? SecondaryColor { get; private set; }
    public string Currency { get; private set; }
    public string Locale { get; private set; }
    public string Timezone { get; private set; }
    public bool IsActive { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public TenantSubscription? Subscription { get; private set; }

    private Tenant() { }

    public static Tenant Create(
        string name,
        string email,
        string phone,
        string subdomainSlug,
        string address,
        string? logoUrl = null,
        string? primaryColor = null,
        string? secondaryColor = null,
        string currency = "TND",
        string locale = "fr-TN",
        string timezone = "Africa/Tunisia"
    )
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email is required.", nameof(email));
        if (string.IsNullOrWhiteSpace(phone))
            throw new ArgumentException("Phone is required.", nameof(phone));
        if (string.IsNullOrWhiteSpace(subdomainSlug))
            throw new ArgumentException("Subdomain slug is required.", nameof(subdomainSlug));
        if (string.IsNullOrWhiteSpace(address))
            throw new ArgumentException("Address is required.", nameof(address));

        return new Tenant
        {
            Id = Guid.NewGuid(),
            Name = name,
            Email = email,
            Phone = phone,
            Slug = subdomainSlug.ToLower(),
            Address = address,
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
        string address,
        string? logoUrl= null,
        string? primaryColor= null,
        string? secondaryColor= null,
        string currency= "TND",
        string locale= "fr-TN",
        string timezone= "Africa/Tunisia")
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email is required.", nameof(email));
        if (string.IsNullOrWhiteSpace(phone))
            throw new ArgumentException("Phone is required.", nameof(phone));
        if (string.IsNullOrWhiteSpace(subdomainSlug))
            throw new ArgumentException("Subdomain slug is required.", nameof(subdomainSlug));
        if (string.IsNullOrWhiteSpace(address))
            throw new ArgumentException("Address is required.", nameof(address));
        Name = name;
        Email = email;
        Phone = phone;
        Slug = subdomainSlug;
        Address = address;
        LogoUrl = logoUrl;
        PrimaryColor = primaryColor;
        SecondaryColor = secondaryColor;
        Currency = currency;
        Locale = locale;
        Timezone = timezone;
    }

    public void Activate() => IsActive = true;

    public void Suspend() => IsActive = false;

    public void Delete()
    {
        if (Subscription is not null)
            throw new InvalidOperationException("Unable to delete tenant: It has an active subscription.");
        IsDeleted = true;
    }
    public void Restore() => IsDeleted = false;

    public void SetSubscription(TenantSubscription subscription) => Subscription = subscription;
    public void AssignSubscription(Guid subscriptionPlanId, DateTime startDate, SubscriptionPeriodEnum period)
    {
        if (IsDeleted) 
            throw new InvalidOperationException("Deleted tenant cannot receive a subscription.");
        if (Subscription is not null)
            throw new InvalidOperationException("Unable to assign new plan, current tenant has an active subscription.");
        Subscription = TenantSubscription.Create(Id, subscriptionPlanId, startDate, period);
    }
    public void RemoveSubscription()
    {
        if (Subscription == null)
            return;
        Subscription = null;
    }

}
