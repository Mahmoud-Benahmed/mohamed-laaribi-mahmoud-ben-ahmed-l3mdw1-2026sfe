using System.ComponentModel.DataAnnotations;

namespace ERP.TenantService.Application.DTOs.Tenant;

public record CreateTenantRequestDto(
    [Required][MaxLength(150)] string Name,
    [Required][EmailAddress][MaxLength(200)] string Email,
    [Required][MaxLength(20)] string Phone,
    [Required][MaxLength(100)] string SubdomainSlug,
    [MaxLength(500)] string? LogoUrl,
    [MaxLength(7)] string? PrimaryColor,
    [MaxLength(7)] string? SecondaryColor,
    [MaxLength(10)] string Currency = "TND",
    [MaxLength(10)] string Locale = "fr-TN",
    [MaxLength(50)] string Timezone = "Africa/Tunisia"
);
