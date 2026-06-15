using ERP.StockService.Application.DTOs;

namespace ERP.StockService.Domain.LocalCache.Article;

public sealed class ArticleCache
{
    public Guid Id { get; private set; }
    public Guid? TenantId { get; private set; }
    public Guid CategoryId { get; private set; }          // ← FK
    public ArticleCategoryCache? Category { get; private set; }  // ← navigation
    public string CodeRef { get; private set; } = default!;
    public string BarCode { get; private set; } = default!;
    public string Libelle { get; private set; } = default!;
    public decimal Prix { get; private set; }
    public string Unit { get; private set; } = default!;
    public decimal TVA { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private ArticleCache() { }

    public static ArticleCache FromEvent(ArticleResponseDto dto) => new()
    {
        Id = dto.Id,
        TenantId = dto.TenantId,
        CategoryId = dto.Category.Id,
        CodeRef = dto.CodeRef,
        BarCode = dto.BarCode,
        Libelle = dto.Libelle,
        Prix = dto.Prix,
        Unit = dto.Unit,
        TVA = dto.TVA,
        IsDeleted = dto.IsDeleted,
        CreatedAt = dto.CreatedAt,
        UpdatedAt = dto.UpdatedAt,
    };

    public static ArticleCache FromEvent(ArticleResponseDto dto, ArticleCategoryCache existingCategory) => new()
    {
        Id = dto.Id,
        Category = existingCategory,          // attach the tracked category
        CategoryId = existingCategory.Id,     // foreign key (optional, EF will infer)
        CodeRef = dto.CodeRef,
        BarCode = dto.BarCode,
        Libelle = dto.Libelle,
        Prix = dto.Prix,
        Unit = dto.Unit,
        TVA = dto.TVA,
        IsDeleted = dto.IsDeleted,
        CreatedAt = dto.CreatedAt,
        UpdatedAt = dto.UpdatedAt,
    };

    public void ApplyUpdate(ArticleResponseDto dto)
    {
        CategoryId = dto.Category.Id;
        CodeRef = dto.CodeRef;
        BarCode = dto.BarCode;
        Libelle = dto.Libelle;
        Prix = dto.Prix;
        Unit = dto.Unit;
        TVA = dto.TVA;
        IsDeleted = dto.IsDeleted;
        UpdatedAt = dto.UpdatedAt;
    }

    public void MarkDeleted() => IsDeleted = true;
    public void MarkRestored() => IsDeleted = false;
}