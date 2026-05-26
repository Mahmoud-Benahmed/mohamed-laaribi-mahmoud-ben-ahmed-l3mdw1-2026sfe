using System.ComponentModel.DataAnnotations;

namespace ERP.ClientService.Application.DTOs
{

    public sealed record CategoryStatsDto(
        int TotalCategories,
        int ActiveCategories,
        int InactiveCategories,
        int DeletedCategories);

    public sealed record CategoryClientCountDto(
        Guid CategoryId,
        string CategoryName,
        int ClientCount);

    public record CreateCategoryRequestDto(
        [Required(ErrorMessage = "Name is required.")]
        [MaxLength(200, ErrorMessage = "Name cannot exceed 200 characters.")]
        [RegularExpression(RegexPatterns.SafeText, ErrorMessage = "Invalid characters.")]
        string Name,

        [Required(ErrorMessage = "Code is required.")]
        [MaxLength(50, ErrorMessage = "Code cannot exceed 50 characters.")]
        [RegularExpression(RegexPatterns.CategoryCode,
        ErrorMessage = "Code can only contain letters, digits, hyphens and underscores.")]
        string Code,

        [Range(7, 270, ErrorMessage = "Return delay must be at least 7 days and not exceed 270 days.")]
        int DelaiRetour,

        [Range(7,180, ErrorMessage = "Due payment period must be at least 7 day and not exceed 180 days.")]
        int DuePaymentPeriod,               // ← added

        bool UseBulkPricing = false,

        [Range(0.0, 1.0, ErrorMessage = "Discount rate must be between 0 and 1 (0% – 100%).")]
        decimal? DiscountRate = null,

        [Range(1, 2, ErrorMessage = "Credit limit multiplier must be positive.")]
        decimal? CreditLimitMultiplier = null
    );

    public record UpdateCategoryRequestDto(
        [Required(ErrorMessage = "Name is required.")]
        [MaxLength(200, ErrorMessage = "Name cannot exceed 200 characters.")]
        [RegularExpression(RegexPatterns.SafeText, ErrorMessage = "Invalid characters.")]
        string Name,

        [Required(ErrorMessage = "Code is required.")]
        [MaxLength(50, ErrorMessage = "Code cannot exceed 50 characters.")]
        [RegularExpression(RegexPatterns.CategoryCode,
        ErrorMessage = "Code can only contain letters, digits, hyphens and underscores.")]
        string Code,

        [Range(7, 270, ErrorMessage = "Return delay must be at least 7 days and not exceed 270 days.")]
        int DelaiRetour,

        [Range(7, 180, ErrorMessage = "Due payment period must be at least 7 days and not exceed 180 days.")]
        int DuePaymentPeriod,

        bool UseBulkPricing = false,

        [Range(0.0, 1.0, ErrorMessage = "Discount rate must be between 0 and 1 (0% – 100%).")]
        decimal? DiscountRate = null,

        [Range(1, 2, ErrorMessage = "Credit limit multiplier must be positive.")]
        decimal? CreditLimitMultiplier = null
    );
    public sealed record CategoryResponseDto(
        Guid Id,
        string Name,
        string Code,
        int DelaiRetour,
        int DuePaymentPeriod,                   // ← added
        decimal? DiscountRate,
        decimal? CreditLimitMultiplier,
        bool UseBulkPricing,
        bool IsActive,
        bool IsDeleted,
        DateTime CreatedAt,
        DateTime? UpdatedAt,
        Guid? TenantId
    );
}