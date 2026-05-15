using System.ComponentModel.DataAnnotations;

namespace ERP.TenantService.Application.DTOs.SubscriptionPlan;

public record UpdateSubscriptionPlanRequestDto(
    [Required][MaxLength(100)] string Name,
    [Required][MaxLength(50)] string Code,
    [Required][Range(0, double.MaxValue)] decimal MonthlyPrice,
    [Required][Range(0, double.MaxValue)] decimal YearlyPrice,
    [Required][Range(1, int.MaxValue)] int MaxUsers,
    [Required][Range(1, int.MaxValue)] int MaxStorageMb
);
