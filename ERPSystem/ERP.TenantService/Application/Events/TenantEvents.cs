namespace ERP.TenantService.Application.Events;

public record TenantCreatedEvent(
    Guid TenantId,
    string Slug,
    bool IsActive
    );
public record TenantUpdatedEvent(
    Guid TenantId, 
    string OldSlug, 
    string NewSlug,
    bool IsActive);

public record TenantDeletedEvent(
    Guid TenantId, 
    string Slug
    );

public record TenantRestoredEvent(
    Guid TenantId, 
    string Slug, 
    bool IsActive);

public record TenantDeactivatedEvent(
    Guid TenantId, 
    string Slug);

public record TenantActivatedEvent(
    Guid TenantId, 
    string Slug
    );



public record SubscriptionChangedEvent(
    Guid TenantId, Guid OldPlanId, Guid NewPlanId,
    int NewMaxUsers, int NewMaxStorageMb);    // downstream adjusts limits

public record SubscriptionExpiredEvent(
    Guid TenantId, 
    Guid PlanId
    );             // triggered by background job