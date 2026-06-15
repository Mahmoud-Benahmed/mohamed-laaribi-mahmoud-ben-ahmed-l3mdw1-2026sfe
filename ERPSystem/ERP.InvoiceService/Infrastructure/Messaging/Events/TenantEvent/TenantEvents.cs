namespace ERP.InvoiceService.Infrastructure.Messaging.Events.TenantEvent;

public record TenantCreatedEvent(
    Guid TenantId,
    string Slug,
    bool IsActive,
    string Name,
    string Address,
    string Email,
    string Phone,
    string Currency,
    string PrimaryColor,
    string SecondaryColor,
    string? LogoUrl
    );
public record TenantUpdatedEvent(
    Guid TenantId,
    bool IsActive,
    string Slug,
    string Name,
    string Address,
    string Email,
    string Phone,
    string Currency,
    string PrimaryColor,
    string SecondaryColor,
    string? LogoUrl
    );

public record TenantDeletedEvent(
    Guid TenantId,
    string Slug
    );