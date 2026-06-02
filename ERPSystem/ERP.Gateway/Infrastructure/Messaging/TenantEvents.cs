namespace ERP.Gateway.Infrastructure.Messaging;
public record TenantCreatedEvent(
    Guid TenantId,
    string Slug,
    bool IsActive
    );
public record TenantUpdatedEvent(
    Guid TenantId,
    string Slug,
    bool IsActive);

public record TenantDeletedEvent(
    Guid TenantId,
    string Slug
    );

public record TenantRestoredEvent(
    Guid TenantId,
    string Slug,
    bool IsActive);

public record TenantSuspendedEvent(
    Guid TenantId,
    string Slug);

public record TenantActivatedEvent(
    Guid TenantId,
    string Slug
    );