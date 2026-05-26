using System.ComponentModel.DataAnnotations;

namespace ERP.ClientService.Application.DTOs;

public sealed record ClientStatsDto(
    int TotalClients,
    int ActiveClients,
    int BlockedClients,
    int DeletedClients,
    List<CategoryClientCountDto> ClientsPerCategory);

public record CreateClientRequestDto(
    [Required(ErrorMessage = "Name is required.")]
    [RegularExpression(RegexPatterns.SafeText, ErrorMessage = "Invalid characters.")]
    [MaxLength(200, ErrorMessage = "Name cannot exceed 200 characters.")]
    string Name,

    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Email is not valid.")]
    [MaxLength(200, ErrorMessage = "Email cannot exceed 200 characters.")]
    string Email,

    [Required(ErrorMessage = "Address is required.")]
    [MaxLength(500, ErrorMessage = "Address cannot exceed 500 characters.")]
    [RegularExpression(RegexPatterns.SafeText, ErrorMessage = "Invalid characters.")]
    string Address,

    [Range(7, 180 , ErrorMessage = "Due payment period must be at least 7 days and not exceed 180 days")]
    int? DuePaymentPeriod = null,

    [Range(7, 270, ErrorMessage = "Return delay must be at least 7 days and not exceed 270 days.")]
    int? DelaiRetour = null,

    [RegularExpression(RegexPatterns.Phone, ErrorMessage = "Phone must contain digits and may start with +.")]
    [MaxLength(20, ErrorMessage = "Phone cannot exceed 20 characters.")]
    string? Phone = null,

    [RegularExpression(RegexPatterns.AlphaNumeric, ErrorMessage = "Invalid tax number.")]
    [MaxLength(50, ErrorMessage = "Tax number cannot exceed 50 characters.")]
    string? TaxNumber = null,

    [Range(1000, double.MaxValue, ErrorMessage = "Credit limit must be positive.")]
    decimal? CreditLimit = null
);

public record UpdateClientRequestDto(
    [Required(ErrorMessage = "Name is required.")]
    [MaxLength(200, ErrorMessage = "Name cannot exceed 200 characters.")]
    [RegularExpression(RegexPatterns.SafeText, ErrorMessage = "Invalid characters.")]
    string Name,

    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Email is not valid.")]
    [MaxLength(200, ErrorMessage = "Email cannot exceed 200 characters.")]
    string Email,

    [Required(ErrorMessage = "Address is required.")]
    [MaxLength(500, ErrorMessage = "Address cannot exceed 500 characters.")]
    [RegularExpression(RegexPatterns.SafeText, ErrorMessage = "Invalid characters.")]
    string Address,

    [Range(7, 180 , ErrorMessage = "Due payment period must be at least 7 day and not exceed 180 days")]
    int? DuePaymentPeriod = null,

    [Range(7, 270, ErrorMessage = "Return delay must be at least 1 day and not exceed 270 days.")]
    int? DelaiRetour = null,

    [RegularExpression(RegexPatterns.Phone, ErrorMessage = "Phone must contain digits and may start with +.")]
    [MaxLength(20, ErrorMessage = "Phone cannot exceed 20 characters.")]
    string? Phone = null,

    [RegularExpression(RegexPatterns.AlphaNumeric, ErrorMessage = "Invalid tax number.")]
    [MaxLength(50, ErrorMessage = "Tax number cannot exceed 50 characters.")]
    string? TaxNumber = null,

    [Range(1000, double.MaxValue, ErrorMessage = "Credit limit must be positive.")]
    decimal? CreditLimit = null
);

public record AddCategoryRequestDto(
    [Required(ErrorMessage = "CategoryId is required.")]
    Guid CategoryId
);

public sealed record ClientResponseDto(
    Guid Id,
    string Name,
    string Email,
    string Address,
    int DuePaymentPeriod,
    string? Phone,
    string? TaxNumber,
    decimal? CreditLimit,
    int? DelaiRetour,
    bool IsBlocked,
    bool IsDeleted,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    List<CategoryResponseDto> Categories,
    Guid? TenantId
);