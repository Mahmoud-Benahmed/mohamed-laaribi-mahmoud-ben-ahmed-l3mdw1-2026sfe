namespace ERP.TenantService.Application.DTOs.Events;

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
    bool IsActive,
    string Slug,
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



public record SubscriptionChangedEvent(
    Guid TenantId, Guid? OldPlanId, Guid NewPlanId,
    int NewMaxUsers, int NewMaxStorageMb);    // downstream adjusts limits

public record SubscriptionExpiredEvent(
    Guid TenantId, 
    Guid PlanId
    );             // triggered by background job