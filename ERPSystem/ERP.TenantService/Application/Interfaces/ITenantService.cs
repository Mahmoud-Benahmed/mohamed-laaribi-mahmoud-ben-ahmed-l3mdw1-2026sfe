using ERP.TenantService.Application.DTOs;
using ERP.TenantService.Application.DTOs.Tenant;
using ERP.TenantService.Application.DTOs.TenantSubscription;
using ERP.TenantService.Domain;

namespace ERP.TenantService.Application.Interfaces;

public interface ITenantService
{
    Task<List<Tenant>> GetAllActiveAsync(CancellationToken ct = default);
    Task<PagedResultDto<TenantResponseDto>> GetAllAsync(int page, int pageSize, CancellationToken ct = default);
    Task<PagedResultDto<TenantResponseDto>> GetDeletedAsync(int page, int pageSize, CancellationToken ct = default);
    Task<TenantResponseDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<TenantResponseDto?> GetBySlugAsync(string slug, CancellationToken ct = default);
    Task<TenantResponseDto> CreateAsync(CreateTenantRequestDto dto, CancellationToken ct = default);
    Task<TenantResponseDto> UpdateAsync(Guid id, UpdateTenantRequestDto dto, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task RestoreAsync(Guid id, CancellationToken ct = default);
    Task ActivateAsync(Guid id, CancellationToken ct = default);
    Task DeactivateAsync(Guid id, CancellationToken ct = default);
    Task<TenantSubscriptionResponseDto> AssignSubscriptionAsync(Guid tenantId, AssignSubscriptionRequestDto dto, CancellationToken ct = default);
    Task RemoveSubscriptionAsync(Guid tenantId, CancellationToken ct = default);
    Task<TenantSubscriptionResponseDto?> GetSubscriptionAsync(Guid tenantId, CancellationToken ct = default);
}
