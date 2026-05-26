using ERP.InvoiceService.Application.DTOs;
using ERP.InvoiceService.Application.Interfaces;
using ERP.InvoiceService.Domain.LocalCache.Article;
using InvoiceService.Application.DTOs;

namespace ERP.InvoiceService.Application.Services.LocalCache.ArticleCache;

public sealed class ArticleCacheService : IArticleCacheService
{
    private readonly IArticleCategoryCacheRepository _categoryRepo;
    private readonly IArticleCategoryCacheService _categoryService;
    private readonly IArticleCacheRepository _repo;
    private readonly ILogger<ArticleCacheService> _logger;

    public ArticleCacheService(
        IArticleCategoryCacheService categoryService,
        IArticleCategoryCacheRepository categoryRepo,
        IArticleCacheRepository repo,
        ILogger<ArticleCacheService> logger)
    {
        _categoryService = categoryService;
        _categoryRepo = categoryRepo;
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
        // 1. Get existing category (or create it)
        ArticleCategoryCache? category = await _categoryRepo.GetByIdAsync(dto.Category.Id)
                       ?? await _categoryRepo.GetByNameAsync(dto.Category.Name);

        if (category == null)
        {
            await _categoryService.SyncCreatedAsync(dto.Category);
            category = await _categoryRepo.GetByIdAsync(dto.Category.Id);
            if (category == null)
                throw new InvalidOperationException($"Category {dto.Category.Id} could not be created.");
        }

        // 2. Check if article already exists
        Domain.LocalCache.Article.ArticleCache? existing = await _repo.GetByIdAsync(dto.Id)
                       ?? await _repo.GetByBarCodeAsync(dto.BarCode)
                       ?? await _repo.GetByCodeRefAsync(dto.CodeRef);

        if (existing != null)
        {
            // Update existing article – use its own method
            existing.ApplyUpdate(dto);
            // Ensure the navigation property points to the tracked category
        }
        else
        {
            // Create new article using the factory overload
            Domain.LocalCache.Article.ArticleCache article = Domain.LocalCache.Article.ArticleCache.FromEvent(dto, category);
            await _repo.AddAsync(article);
        }

        await _repo.SaveChangesAsync();
    }

    public async Task SyncUpdatedAsync(ArticleResponseDto dto)
    {
        Domain.LocalCache.Article.ArticleCache? existing = await _repo.GetByIdAsync(dto.Id);
        if (existing is null)
        {
            _logger.LogWarning(
                "SyncUpdated: article {Id} not in cache, inserting instead", dto.Id);
            await _repo.AddAsync(Domain.LocalCache.Article.ArticleCache.FromEvent(dto));
        }
        else
        {
            existing.ApplyUpdate(dto);
        }

        await _repo.SaveChangesAsync();
        _logger.LogInformation("ArticleCache synced (updated) for {Id} — {Libelle}", dto.Id, dto.Libelle);
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
        Domain.LocalCache.Article.ArticleCache? existing = await _repo.GetByIdAsync(dto.Id);
        if (existing is null)
        {
            _logger.LogWarning("SyncRestored: article {Id} not in cache, inserting instead", dto.Id);
            await _repo.AddAsync(Domain.LocalCache.Article.ArticleCache.FromEvent(dto));
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

    private static ArticleCategoryResponseDto MapCategoryToDto(ArticleCategoryCache? c) => c is null
        ? new ArticleCategoryResponseDto(Guid.Empty, string.Empty, 0, false, DateTime.MinValue, null, null)
        : new ArticleCategoryResponseDto(
            Id: c.Id,
            Name: c.Name,
            TVA: c.TVA,
            IsDeleted: c.IsDeleted,
            CreatedAt: c.CreatedAt,
            UpdatedAt: c.UpdatedAt,
            TenantId: c.TenantId);
}