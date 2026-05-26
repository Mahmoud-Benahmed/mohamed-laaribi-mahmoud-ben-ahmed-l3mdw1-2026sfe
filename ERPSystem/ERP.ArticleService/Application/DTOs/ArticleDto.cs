using System.ComponentModel.DataAnnotations;

namespace ERP.ArticleService.Application.DTOs
{
    public record CreateArticleRequestDto(
        [Required(ErrorMessage = "Libelle is required.")]
        [MaxLength(200, ErrorMessage = "Libelle cannot exceed 200 characters.")]
        [RegularExpression(RegexPatterns.SafeText, ErrorMessage ="Invalid characters")]
        string Libelle,

        [Required(ErrorMessage = "Prix is required.")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Prix must be greater than zero.")]
        decimal Prix,

        [Required(ErrorMessage = "Unit is required.")]
        UnitEnum Unit,

        [Required(ErrorMessage = "CategoryId is required.")]
        Guid CategoryId,

        [Required(ErrorMessage = "BarCode is required.")]
        [StringLength(13, MinimumLength = 8, ErrorMessage = "BarCode must be between 8 and 13 characters.")]
        [RegularExpression(RegexPatterns.BarCode, ErrorMessage ="BarCode must be 8–13 digits.")]
        string BarCode,

        [Range(0, 100, ErrorMessage = "TVA must be between 0 and 100.")]
        int? TVA
    );

    public record UpdateArticleRequestDto(
        [Required(ErrorMessage = "Libelle is required.")]
        [RegularExpression(RegexPatterns.SafeText, ErrorMessage ="Invalid characters")]
        [MaxLength(200, ErrorMessage = "Libelle cannot exceed 200 characters.")]
        string Libelle,

        [Required(ErrorMessage = "Prix is required.")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Prix must be greater than zero.")]
        decimal Prix,

        [Required(ErrorMessage = "Unit is required.")]
        UnitEnum Unit,

        [Required(ErrorMessage = "CategoryId is required.")]
        Guid CategoryId,

        [StringLength(13, MinimumLength = 8, ErrorMessage = "BarCode must be between 8 and 13 characters.")]
        [RegularExpression(RegexPatterns.BarCode, ErrorMessage ="BarCode must be 8–13 digits.")]
        string BarCode,

        [Range(0, 100, ErrorMessage = "TVA must be between 0 and 100 (0% – 100%).")]
        int? TVA
    );

    public record ArticleStatsDto(
        int TotalCount,
        int ActiveCount,
        int DeletedCount,
        int CategoriesCount
    );

    public record ArticleResponseDto(
        Guid Id,
        CategoryResponseDto Category,
        string CodeRef,
        string BarCode,
        string Libelle,
        decimal Prix,
        string Unit,
        int TVA,
        bool IsDeleted,
        DateTime CreatedAt,
        DateTime? UpdatedAt,
        Guid? TenantId
        );
}
