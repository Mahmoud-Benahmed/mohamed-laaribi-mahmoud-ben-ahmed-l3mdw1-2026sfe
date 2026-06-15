using System.ComponentModel.DataAnnotations;
namespace ERP.ArticleService.Application.DTOs
{

    public record CategoryRequestDto(
        [Required(ErrorMessage = "Category name is required")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Name must be between 2 and 100 characters")]
        [RegularExpression(RegexPatterns.SafeText, ErrorMessage ="Invalid characters")]
        string Name,

        [Range(0, 100, ErrorMessage = "TVA must be between 0 and 100 (0% – 100%).")]
        int TVA
    );

    public record CategoryResponseDto(
        Guid Id,
        string Name,
        int TVA,
        bool IsDeleted,
        DateTime CreatedAt,
        DateTime? UpdatedAt,
        Guid? TenantId
    );

    public sealed record CategoryStatsDto(
        int ActiveCategories,
        int DeletedCategories
    );

}
