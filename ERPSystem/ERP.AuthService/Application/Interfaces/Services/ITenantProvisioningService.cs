using ERP.AuthService.Infrastructure.Messaging.Events.TenantEvent;

namespace ERP.AuthService.Application.Interfaces.Services;

public interface ITenantProvisioningService
{
    Task ProvisionAsync(Guid tenantId, string slug);
    Task DeleteAllByTenantIdAsync(Guid tenantId);
}