using ERP.InvoiceService.Application.DTOs;
using ERP.InvoiceService.Application.Interfaces;
using ERP.InvoiceService.Domain.LocalCache.Article;
using InvoiceService.Application.DTOs;

namespace ERP.InvoiceService.Application.Services.LocalCache.ArticleCache;

public sealed class ArticleCacheService : IArticleCacheService
{
    private readonly IArticleCacheRepository _repo;
    private readonly ILogger<ArticleCacheService> _logger;

    public ArticleCacheService(
        IArticleCacheRepository repo,
        ILogger<ArticleCacheService> logger)
    {
        _repo = repo;
        _logger = logger;
    }

    // ── Queries ───────────────────────────────────────────────────────────────

    public async Task<ArticleResponseDto?> GetByIdAsync(Guid id)
    {
        Domain.LocalCache.Article.ArticleCache? article = await _repo.GetByIdAsync(id);
        return article is null ? null : MapToDto(article);
    }

    public async Task<ArticleResponseDto?> GetByBarCodeAsync(string barCode)
    {
        Domain.LocalCache.Article.ArticleCache? article = await _repo.GetByBarCodeAsync(barCode);
        return article is null ? null : MapToDto(article);
    }

    public async Task<ArticleResponseDto?> GetByCodeRefAsync(string codeRef)
    {
        Domain.LocalCache.Article.ArticleCache? article = await _repo.GetByCodeRefAsync(codeRef);
        return article is null ? null : MapToDto(article);
    }

    public async Task<List<ArticleResponseDto>> GetAllAsync()
    {
        List<Domain.LocalCache.Article.ArticleCache> articles = await _repo.GetAllAsync();
        return articles.Select(MapToDto).ToList();
    }

    public async Task<List<ArticleResponseDto>> GetAllActiveAsync()
    {
        List<Domain.LocalCache.Article.ArticleCache> articles = await _repo.GetAllActiveAsync();
        return articles.Select(MapToDto).ToList();
    }

    public async Task<PagedResultDto<ArticleResponseDto>> GetPagedAsync(int pageNumber, int pageSize, string? search = null)
    {
        if (pageNumber < 1) throw new ArgumentOutOfRangeException(nameof(pageNumber));
        if (pageSize < 1) throw new ArgumentOutOfRangeException(nameof(pageSize));

        (List<Domain.LocalCache.Article.ArticleCache>? items, int totalCount) = await _repo.GetPagedAsync(pageNumber, pageSize, search);

        return new PagedResultDto<ArticleResponseDto>(
            items.Select(MapToDto).ToList(),
            totalCount,
            pageNumber,
            pageSize);
    }

    // ── Kafka sync ────────────────────────────────────────────────────────────

    public async Task SyncCreatedAsync(ArticleResponseDto dto)
    {
        Domain.LocalCache.Article.ArticleCache? existing = await _repo.GetByIdAsync(dto.Id);

        if (existing == null)
        {
            // Create new article using the factory overload
            Domain.LocalCache.Article.ArticleCache article = Domain.LocalCache.Article.ArticleCache.FromEvent(dto);
            await _repo.AddAsync(article);
            await _repo.SaveChangesAsync();
        }
        else
        {
            _logger.LogWarning(
                "SyncUpdated: article {Id} existing in cache, create process will be cancelled", dto.Id);
        }

    }

    public async Task SyncUpdatedAsync(ArticleResponseDto dto)
    {
        Domain.LocalCache.Article.ArticleCache? existing = await _repo.GetByIdAsync(dto.Id);
        if (existing is null)
        {
            _logger.LogWarning(
                "SyncUpdated: article {Id} not existing in cache, update process will be cancelled", dto.Id);
        }
        else
        {
            existing.ApplyUpdate(dto);
            await _repo.UpdateAsync(existing);
            await _repo.SaveChangesAsync();
            _logger.LogInformation("ArticleCache synced (updated) for {Id} — {Libelle}", dto.Id, dto.Libelle);
        }
    }

    public async Task SyncDeletedAsync(ArticleResponseDto dto)
    {
        Domain.LocalCache.Article.ArticleCache? existing = await _repo.GetByIdAsync(dto.Id);
        if (existing is null)
        {
            _logger.LogWarning("SyncDeleted: article {Id} not in cache, skipping", dto.Id);
            return;
        }

        existing.MarkDeleted();
        await _repo.SaveChangesAsync();
        _logger.LogInformation("ArticleCache marked deleted for {Id}", dto.Id);
    }

    public async Task SyncRestoredAsync(ArticleResponseDto dto)
    {
        Domain.LocalCache.Article.ArticleCache? existing = await _repo.GetByIdDeletedAsync(dto.Id);
        if (existing is null)
        {
            _logger.LogWarning("SyncRestored: article {Id} not in cache, inserting instead", dto.Id);
            return;
        }
        else
        {
            existing.MarkRestored();
        }

        await _repo.SaveChangesAsync();
        _logger.LogInformation("ArticleCache marked restored for {Id}", dto.Id);
    }

    // ── Mapping ───────────────────────────────────────────────────────────────

    private static ArticleResponseDto MapToDto(Domain.LocalCache.Article.ArticleCache a) => new(
        Id: a.Id,
        Category: MapCategoryToDto(a.Category),
        CodeRef: a.CodeRef,
        BarCode: a.BarCode,
        Libelle: a.Libelle,
        Prix: a.Prix,
        Unit: a.Unit,
        TVA: a.TVA,
        IsDeleted: a.IsDeleted,
        CreatedAt: a.CreatedAt,
        UpdatedAt: a.UpdatedAt,
        TenantId: a.TenantId);

    private static ArticleCategoryResponseDto? MapCategoryToDto(ArticleCategoryCache? c) =>
        c is null ? null : new(
            Id: c.Id,
            Name: c.Name,
            TVA: c.TVA,
            IsDeleted: c.IsDeleted,
            CreatedAt: c.CreatedAt,
            UpdatedAt: c.UpdatedAt,
            TenantId: c.TenantId
        );
}