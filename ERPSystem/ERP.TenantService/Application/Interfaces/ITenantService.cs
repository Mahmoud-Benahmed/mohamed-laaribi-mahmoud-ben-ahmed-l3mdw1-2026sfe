using ERP.TenantService.Application.DTOs.Tenant;
using ERP.TenantService.Application.DTOs.TenantSubscription;

namespace ERP.TenantService.Application.Interfaces;

public interface ITenantService
{
    Task<(IEnumerable<TenantResponseDto> Items, int TotalCount)> GetAllAsync(int page, int pageSize);
    Task<TenantResponseDto?> GetByIdAsync(Guid id);
    Task<TenantResponseDto?> GetBySubdomainSlugAsync(string slug);
    Task<TenantResponseDto> CreateAsync(CreateTenantRequestDto dto);
    Task<TenantResponseDto> UpdateAsync(Guid id, UpdateTenantRequestDto dto);
    Task DeleteAsync(Guid id);
    Task ActivateAsync(Guid id);
    Task DeactivateAsync(Guid id);
    Task<TenantSubscriptionResponseDto> AssignSubscriptionAsync(Guid tenantId, AssignSubscriptionRequestDto dto);
    Task<TenantSubscriptionResponseDto?> GetSubscriptionAsync(Guid tenantId);
}
