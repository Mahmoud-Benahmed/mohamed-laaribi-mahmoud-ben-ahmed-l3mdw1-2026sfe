namespace ERP.Gateway.Infrastructure.Messaging;

public static class TenantTopics
{
    // Tenant lifecycle
    public const string TenantCreated = "tenant.created";
    public const string TenantUpdated = "tenant.updated";
    public const string TenantDeleted = "tenant.deleted";
    public const string TenantRestored = "tenant.restored";
    public const string TenantActivated = "tenant.activated";
    public const string TenantDeactivated = "tenant.deactivated";
}