using ERP.InvoiceService.Infrastructure.Messaging.Events.TenantEvent;

namespace ERP.InvoiceService.Domain.LocalCache.Tenant;

public class TenantCache
{
    public Guid TenantId { get; private set; }
    public string Slug {get; private set;}
    public bool IsActive {get; private set;}
    public string Name {get; private set;}
    public string Address {get; private set;}
    public string Email {get; private set;}
    public string Phone {get; private set;}
    public string Currency {get; private set;}
    public string PrimaryColor { get; private set; }
    public string SecondaryColor { get; private set; }
    public string? LogoUrl { get; private set; }

    private TenantCache() { }
    public static TenantCache Create(TenantCreatedEvent evt)
    {
        return new TenantCache
        {
            TenantId = evt.TenantId,
            Slug = evt.Slug,
            IsActive = evt.IsActive,
            Name = evt.Name,
            Address = evt.Address,
            Email = evt.Email,
            Phone = evt.Phone,
            Currency = evt.Currency,
            PrimaryColor= evt.PrimaryColor,
            SecondaryColor= evt.SecondaryColor,
            LogoUrl= evt.LogoUrl
        };
    }

    public void Update(TenantUpdatedEvent evt)
    {
        Slug = evt.Slug;
        IsActive = evt.IsActive;
        Name = evt.Name;
        Address = evt.Address;
        Email = evt.Email;
        Phone = evt.Phone;
        Currency = evt.Currency;
        PrimaryColor = evt.PrimaryColor;
        SecondaryColor = evt.SecondaryColor;
        LogoUrl = evt.LogoUrl;
    }
}
