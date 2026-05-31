namespace ERP.AuthService.Infrastructure.Messaging.Events.TenantEvent;

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
    string SecondaryColor
    );
public record TenantUpdatedEvent(
    Guid TenantId,
    string OldSlug,
    string NewSlug,
    bool IsActive,
    string Name,
    string Address,
    string Email,
    string Phone,
    string Currency,
    string PrimaryColor,
    string SecondaryColor);

public record TenantDeletedEvent(
    Guid TenantId,
    string Slug
    );