namespace ERP.ClientService.Infrastructure.Messaging;

public static class TenantTopics
{
    // Tenant lifecycle
    public const string TenantCreated = "tenant.created";
    public const string TenantDeleted = "tenant.deleted";

}