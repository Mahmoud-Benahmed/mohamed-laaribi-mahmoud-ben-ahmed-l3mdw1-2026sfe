namespace ERP.TenantService.Application.Events;


public record SubscriptionAssignedEvent(
    Guid TenantId,
    Guid OldPlanId,
    Guid NewPlanId,
    string NewPlanCode,
    int NewMaxUsers,
    int NewMaxStorageMb,
    DateTime StartDate,
    DateTime EndDate);

public record SubscriptionPlanCreatedEvent(
    Guid PlanId, string Name, string Code,
    int MaxUsers, int MaxStorageMb);

public record SubscriptionPlanUpdatedEvent(
    Guid PlanId, string Name, string Code,
    int MaxUsers, int MaxStorageMb);

public record SubscriptionPlanDeletedEvent(
    Guid PlanId, string Code);
