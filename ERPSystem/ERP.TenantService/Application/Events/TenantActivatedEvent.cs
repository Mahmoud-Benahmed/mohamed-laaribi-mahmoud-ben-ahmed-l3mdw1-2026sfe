namespace ERP.TenantService.Application.Events;

public record TenantActivatedEvent(
    Guid TenantId,
    string TenantName,
    string SubdomainSlug
);