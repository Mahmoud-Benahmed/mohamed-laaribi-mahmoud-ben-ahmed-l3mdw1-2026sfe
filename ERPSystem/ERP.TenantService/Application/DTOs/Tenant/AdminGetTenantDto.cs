using System.ComponentModel.DataAnnotations;

namespace ERP.TenantService.Application.DTOs.Tenant;

public record GetTenantSettingsDto(
    string Name,
    string Email,
    string Phone,
    string Address,
    string Slug,
    string? LogoUrl,
    string PrimaryColor,
    string SecondaryColor,
    string Currency,
    string Locale,
    string Timezone
    );
