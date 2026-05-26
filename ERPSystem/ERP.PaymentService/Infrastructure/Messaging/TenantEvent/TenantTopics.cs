namespace ERP.PaymentService.Infrastructure.Messaging.TenantEvent;

public static class TenantTopics
{
    // Tenant lifecycle
    public const string TenantCreated = "tenant.created";
    public const string TenantDeleted = "tenant.deleted";

}