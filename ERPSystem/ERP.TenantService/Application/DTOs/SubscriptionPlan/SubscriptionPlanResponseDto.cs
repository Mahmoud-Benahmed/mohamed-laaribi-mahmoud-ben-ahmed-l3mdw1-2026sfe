namespace ERP.TenantService.Application.DTOs.SubscriptionPlan;

public record SubscriptionPlanResponseDto(
    Guid Id,
    string Name,
    string Code,
    decimal MonthlyPrice,
    decimal YearlyPrice,
    int MaxUsers,
    int MaxStorageMb,
    bool IsActive
);
