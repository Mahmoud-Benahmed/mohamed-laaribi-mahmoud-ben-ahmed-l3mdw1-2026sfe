using ERP.TenantService.Application.DTOs.SubscriptionPlan;

namespace ERP.TenantService.Application.DTOs.TenantSubscription;

public record TenantSubscriptionResponseDto(
    Guid TenantId,
    DateTime StartDate,
    DateTime EndDate,
    SubscriptionPlanResponseDto? Plan
);
