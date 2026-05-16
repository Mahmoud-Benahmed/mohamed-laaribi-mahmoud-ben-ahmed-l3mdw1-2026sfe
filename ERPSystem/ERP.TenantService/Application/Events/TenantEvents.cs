namespace ERP.TenantService.Application.Events;

public record TenantEvents(
    Guid TenantId,
    string TenantName,
    string SubdomainSlug
);
// Events to add
public record TenantCreatedEvent(
    Guid TenantId, string Name, string SubdomainSlug,
    Guid PlanId, string PlanCode,
    int MaxUsers, int MaxStorageMb);          // downstream creates DB schema, storage bucket, etc.

public record TenantDeletedEvent(
    Guid TenantId, string SubdomainSlug);     // downstream cleans up resources

public record TenantRestoredEvent(
    Guid TenantId, string SubdomainSlug);

public record TenantDeactivatedEvent(
    Guid TenantId, string SubdomainSlug);     // downstream blocks logins

public record SubscriptionChangedEvent(
    Guid TenantId, Guid OldPlanId, Guid NewPlanId,
    int NewMaxUsers, int NewMaxStorageMb);    // downstream adjusts limits

public record SubscriptionExpiredEvent(
    Guid TenantId, Guid PlanId);             // triggered by background job

public record TenantUpdatedEvent(
    Guid TenantId, string Name, string SubdomainSlug);

public record TenantActivatedEvent(
    Guid TenantId, string Name, string SubdomainSlug);
