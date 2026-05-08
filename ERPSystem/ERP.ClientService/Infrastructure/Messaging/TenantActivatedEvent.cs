namespace ERP.ClientService.Infrastructure.Messaging;

public record TenantActivatedEvent(
    Guid TenantId,
    string TenantName,
    string SubdomainSlug
);