using ERP.TenantService.Application.DTOs.SubscriptionPlan;

namespace ERP.TenantService.Application.Interfaces;

public interface ISubscriptionPlanService
{
    Task<IEnumerable<SubscriptionPlanResponseDto>> GetAllAsync();
    Task<SubscriptionPlanResponseDto?> GetByIdAsync(Guid id);
    Task<SubscriptionPlanResponseDto> CreateAsync(CreateSubscriptionPlanRequestDto dto);
    Task<SubscriptionPlanResponseDto> UpdateAsync(Guid id, UpdateSubscriptionPlanRequestDto dto);
    Task ActivateAsync(Guid id);
    Task DeactivateAsync(Guid id);
}
