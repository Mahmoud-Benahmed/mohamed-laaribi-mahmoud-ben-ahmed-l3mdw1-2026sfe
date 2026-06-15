using ERP.AuthService.Infrastructure.Messaging.Events.TenantEvent;
using ERP.AuthService.Infrastructure.Persistence.Repositories;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ERP.AuthService.Domain.Cache;

public class TenantCache : ITenantFilterable
{
    [BsonId]
    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid Id { get; private set; }

    Guid? ITenantFilterable.TenantId => Id;

    public string Slug { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public string Address { get; private set; } = default!;
    public string Email { get; private set; } = default!;
    public string Phone { get; private set; } = default!;
    public string Currency { get; private set; } = default!;
    public string? PrimaryColor { get; private set; }
    public string? SecondaryColor { get; private set; }
    public bool IsActive { get; private set; }

    // ── EF / MongoDB constructor ──────────────────────────────────────────────
    private TenantCache() { }

    // ── Factory ───────────────────────────────────────────────────────────────
    public static TenantCache FromEvent(TenantCreatedEvent e) => new()
    {
        Id = e.TenantId,
        Slug = e.Slug,
        Name = e.Name,
        Address = e.Address,
        Email = e.Email,
        Phone = e.Phone,
        Currency = e.Currency,
        PrimaryColor = e.PrimaryColor,
        SecondaryColor = e.SecondaryColor,
        IsActive = e.IsActive,
    };

    // ── Mutators ──────────────────────────────────────────────────────────────
    public void ApplyUpdate(TenantCreatedEvent e)
    {
        Slug = e.Slug;
        Name = e.Name;
        Address = e.Address;
        Email = e.Email;
        Phone = e.Phone;
        Currency = e.Currency;
        PrimaryColor = e.PrimaryColor;
        SecondaryColor = e.SecondaryColor;
        IsActive = e.IsActive;
    }

    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;
}