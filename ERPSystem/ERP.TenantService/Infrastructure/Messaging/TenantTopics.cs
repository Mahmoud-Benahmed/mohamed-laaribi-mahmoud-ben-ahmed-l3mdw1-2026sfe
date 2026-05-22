namespace ERP.TenantService.Infrastructure.Messaging;

public static class TenantTopics
{
    // Tenant lifecycle
    public const string TenantCreated = "tenant.created";
    public const string TenantUpdated = "tenant.updated";
    public const string TenantDeleted = "tenant.deleted";
    public const string TenantRestored = "tenant.restored";
    public const string TenantActivated = "tenant.activated";
    public const string TenantSuspended = "tenant.suspended";

    // Subscription
    public const string SubscriptionAssigned = "tenant.subscription.assigned";
    public const string SubscriptionExpired = "tenant.subscription.expired";
    public const string SubscriptionPlanCreated = "tenant.subscription.plan.created";
    public const string SubscriptionPlanUpdated = "tenant.subscription.plan.updated";
    public const string SubscriptionPlanDeleted = "tenant.subscription.plan.deleted";
}