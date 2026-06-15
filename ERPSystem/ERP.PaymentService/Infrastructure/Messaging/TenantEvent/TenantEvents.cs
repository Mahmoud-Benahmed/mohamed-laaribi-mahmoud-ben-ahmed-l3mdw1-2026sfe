namespace ERP.PaymentService.Infrastructure.Messaging.TenantEvent;
public record TenantCreatedEvent(
    Guid TenantId,
    string Slug,
    bool IsActive
    );
public record TenantDeletedEvent(
    Guid TenantId,
    string Slug
    );