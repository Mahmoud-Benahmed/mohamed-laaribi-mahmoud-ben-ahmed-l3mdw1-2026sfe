namespace ERP.AuthService.Application.Interfaces;

public interface ITenantServiceClient
{
    Task<string?> GetSlugByIdAsync(Guid tenantId);
}