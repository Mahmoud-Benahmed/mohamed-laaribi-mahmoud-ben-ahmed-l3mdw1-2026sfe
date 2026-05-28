using ERP.TenantService.Application.DTOs.TenantSubscription;
using System.ComponentModel.DataAnnotations;

namespace ERP.TenantService.Application.DTOs.Tenant;

public record CreateTenantRequestDto(
    [Required][MaxLength(150)] string Name,
    [Required][EmailAddress][MaxLength(200)] string Email,
    [Required][RegularExpression(RegexPatterns.Phone, ErrorMessage = "Phone must contain digits and may start with +.")] string Phone,
    [Required] AssignSubscriptionRequestDto Subscription,
    [Required][RegularExpression(RegexPatterns.SafeText, ErrorMessage = "Invalid characters.")][MaxLength(100)] string SubdomainSlug,
    [Required][RegularExpression(RegexPatterns.SafeText, ErrorMessage = "Invalid characters.")] string Address,
    [MaxLength(500)] string? LogoUrl,
    [MaxLength(7)] string? PrimaryColor,
    [MaxLength(7)] string? SecondaryColor,
    [MaxLength(10)] string Currency = "TND",
    [MaxLength(10)] string Locale = "fr-TN",
    [MaxLength(50)] string Timezone = "Africa/Tunisia"
);