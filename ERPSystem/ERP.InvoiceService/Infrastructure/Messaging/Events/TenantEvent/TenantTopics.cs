namespace ERP.InvoiceService.Infrastructure.Messaging.Events.TenantEvent;

public static class TenantTopics
{
    // Tenant lifecycle
    public const string TenantCreated = "tenant.created";
    public const string TenantUpdated = "tenant.updated";
    public const string TenantDeleted = "tenant.deleted";

}