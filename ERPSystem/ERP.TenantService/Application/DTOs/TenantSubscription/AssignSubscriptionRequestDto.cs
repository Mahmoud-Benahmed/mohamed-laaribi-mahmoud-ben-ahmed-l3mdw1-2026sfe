using ERP.TenantService.Domain;
using System.ComponentModel.DataAnnotations;

namespace ERP.TenantService.Application.DTOs.TenantSubscription;

public record AssignSubscriptionRequestDto(
    [Required] Guid SubscriptionPlanId,
    [Required] DateTime StartDate,
    [Required] SubscriptionPeriodEnum Period
);