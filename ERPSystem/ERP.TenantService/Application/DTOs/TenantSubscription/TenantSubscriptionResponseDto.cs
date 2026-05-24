using ERP.TenantService.Application.DTOs.SubscriptionPlan;

namespace ERP.TenantService.Application.DTOs.TenantSubscription;

public record TenantSubscriptionResponseDto(
    Guid TenantId,
    DateTimeOffset StartDate,
    DateTimeOffset EndDate,
    SubscriptionPlanResponseDto? Plan
);
