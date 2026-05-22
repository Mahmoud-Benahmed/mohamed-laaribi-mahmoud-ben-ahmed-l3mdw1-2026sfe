namespace ERP.AuthService.Infrastructure.Messaging;
public record TenantCreatedEvent(
    Guid TenantId,
    string Slug,
    bool IsActive
    );