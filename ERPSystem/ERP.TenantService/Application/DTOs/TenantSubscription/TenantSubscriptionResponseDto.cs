using ERP.TenantService.Application.DTOs.SubscriptionPlan;
using ERP.TenantService.Domain;

namespace ERP.TenantService.Application.DTOs.TenantSubscription;

public record TenantSubscriptionResponseDto(
    Guid TenantId,
    DateTimeOffset StartDate,
    DateTimeOffset EndDate,
    SubscriptionPeriodEnum Period,
    SubscriptionPlanResponseDto? Plan
);
