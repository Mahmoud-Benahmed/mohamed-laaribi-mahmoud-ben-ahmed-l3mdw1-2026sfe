using ERP.TenantService.Application.DTOs;
using ERP.TenantService.Application.DTOs.SubscriptionPlan;

namespace ERP.TenantService.Application.Interfaces.Services;

public interface ISubscriptionPlanService
{
    Task<PagedResultDto<SubscriptionPlanResponseDto>> GetAllAsync(int page = 1, int pageSize = 10, CancellationToken ct = default);
    Task<SubscriptionPlanResponseDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<SubscriptionPlanResponseDto> CreateAsync(CreateSubscriptionPlanRequestDto dto, CancellationToken ct = default);
    Task<SubscriptionPlanResponseDto> UpdateAsync(Guid id, UpdateSubscriptionPlanRequestDto dto, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task ActivateAsync(Guid id, CancellationToken ct = default);
    Task DeactivateAsync(Guid id, CancellationToken ct = default);
}
