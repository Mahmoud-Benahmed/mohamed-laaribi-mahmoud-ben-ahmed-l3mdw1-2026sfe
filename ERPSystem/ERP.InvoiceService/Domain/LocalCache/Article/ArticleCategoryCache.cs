using ERP.InvoiceService.Application.DTOs;

namespace ERP.InvoiceService.Domain.LocalCache.Article;

public sealed class ArticleCategoryCache
{
    public Guid Id { get; private set; }
    public Guid? TenantId { get; private set; }
    public string Name { get; private set; } = default!;
    public decimal TVA { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private ArticleCategoryCache() { }
    public static ArticleCategoryCache FromEvent(ArticleCategoryResponseDto dto) => new()
    {
        Id = dto.Id,
        TenantId = dto.TenantId,
        Name = dto.Name,
        TVA = dto.TVA,
        IsDeleted = dto.IsDeleted,
        CreatedAt = dto.CreatedAt,
        UpdatedAt = dto.UpdatedAt,
    };

    public void ApplyUpdate(ArticleCategoryResponseDto dto)
    {
        Name = dto.Name;
        TVA = dto.TVA;
        IsDeleted = dto.IsDeleted;
        UpdatedAt = dto.UpdatedAt;
    }

    public void MarkDeleted() => IsDeleted = true;
    public void MarkRestored() => IsDeleted = false;

}