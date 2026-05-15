using System.ComponentModel.DataAnnotations;

namespace ERP.TenantService.Application.DTOs.Tenant;

public record UpdateTenantRequestDto(
    [Required][MaxLength(150)] string Name,
    [Required][EmailAddress][MaxLength(200)] string Email,
    [Required][MaxLength(20)] string Phone,
    [Required][MaxLength(100)] string SubdomainSlug,
    [MaxLength(500)] string? LogoUrl,
    [MaxLength(7)] string? PrimaryColor,
    [MaxLength(7)] string? SecondaryColor,
    [Required][MaxLength(10)] string Currency,
    [Required][MaxLength(10)] string Locale,
    [Required][MaxLength(50)] string Timezone
);
