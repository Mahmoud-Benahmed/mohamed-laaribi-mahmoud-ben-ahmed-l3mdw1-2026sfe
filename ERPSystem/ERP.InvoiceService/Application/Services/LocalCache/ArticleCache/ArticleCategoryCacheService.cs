using ERP.InvoiceService.Application.DTOs;
using ERP.InvoiceService.Application.Interfaces;
using ERP.InvoiceService.Domain.LocalCache.Article;
using InvoiceService.Application.DTOs;
using Microsoft.EntityFrameworkCore;

namespace ERP.InvoiceService.Application.Services.LocalCache.ArticleCache;

public sealed class ArticleCategoryCacheService : IArticleCategoryCacheService
{
    private readonly IArticleCategoryCacheRepository _repo;
    private readonly ILogger<ArticleCategoryCacheService> _logger;
    private readonly ITenantContext _tenantContext;

    public ArticleCategoryCacheService(
        IArticleCategoryCacheRepository repo,
        ILogger<ArticleCategoryCacheService> logger,
        ITenantContext tenantContext)
    {
        _repo = repo;
        _logger = logger;
        _tenantContext = tenantContext;
    }

    // ── Queries ───────────────────────────────────────────────────────────────

    public async Task<ArticleCategoryResponseDto?> GetByIdAsync(Guid id)
    {
        ArticleCategoryCache? category = await _repo.GetByIdAsync(id);
        return category is null ? null : MapToDto(category);
    }

    public async Task<ArticleCategoryResponseDto?> GetByNameAsync(string name)
    {
        ArticleCategoryCache? category = await _repo.GetByNameAsync(name);
        return category is null ? null : MapToDto(category);
    }


    public async Task<List<ArticleCategoryResponseDto>> GetAllAsync()
    {
        List<ArticleCategoryCache> categories = await _repo.GetAllAsync();
        return categories.Select(MapToDto).ToList();
    }

    public async Task<List<ArticleCategoryResponseDto>> GetAllActiveAsync()
    {
        List<ArticleCategoryCache> categories = await _repo.GetAllActiveAsync();
        return categories.Select(MapToDto).ToList();
    }

    public async Task<PagedResultDto<ArticleCategoryResponseDto>> GetPagedAsync(int pageNumber, int pageSize)
    {
        if (pageNumber < 1) throw new ArgumentOutOfRangeException(nameof(pageNumber));
        if (pageSize < 1) throw new ArgumentOutOfRangeException(nameof(pageSize));

        (List<ArticleCategoryCache>? items, int totalCount) = await _repo.GetPagedAsync(pageNumber, pageSize);
        return new PagedResultDto<ArticleCategoryResponseDto>(
            items.Select(MapToDto).ToList(),
            totalCount,
            pageNumber,
            pageSize);
    }

    public async Task<bool> ExistsAsync(string name)
    {
        return await _repo.ExistsAsync(name);
    }

    // ── Kafka sync ────────────────────────────────────────────────────────────

    // CategoryCacheService.cs
    public async Task SyncCreatedAsync(ArticleCategoryResponseDto dto)
    {
        ArticleCategoryCache? existing = await _repo.GetByIdAsync(dto.Id);
        if (existing is not null)
        {
            _logger.LogWarning("SyncCreated: category {Id} already exists, skipping", dto.Id);
            return;
        }

        await _repo.AddAsync(ArticleCategoryCache.FromEvent(dto));
        await _repo.SaveChangesAsync();
        _logger.LogInformation("Created category cache for {Id} — {Name}", dto.Id, dto.Name);
    }

    public async Task SyncUpdatedAsync(ArticleCategoryResponseDto dto)
    {
        ArticleCategoryCache? existing = await _repo.GetByIdAsync(dto.Id);
        if (existing is null)
        {
            _logger.LogWarning("SyncUpdated: category {Id} not in cache, inserting instead", dto.Id);
            await _repo.AddAsync(ArticleCategoryCache.FromEvent(dto));
        }
        else
        {
            existing.ApplyUpdate(dto);
            await _repo.UpdateAsync(existing);
        }

        await _repo.SaveChangesAsync();
        _logger.LogInformation("ArticleCache synced (updated) for {Id} — {Libelle}", dto.Id, dto.Name);
    }

    public async Task SyncDeletedAsync(ArticleCategoryResponseDto dto)
    {
        ArticleCategoryCache? existing = await _repo.GetByIdAsync(dto.Id);
        if (existing is null)
        {
            _logger.LogWarning("SyncDeleted: category {Id} not in cache, skipping", dto.Id);
            return;
        }

        existing.MarkDeleted();
        await _repo.UpdateAsync(existing);
        await _repo.SaveChangesAsync();
        _logger.LogInformation("ArticleCache marked deleted for {Id}", dto.Id);
    }

    public async Task SyncRestoredAsync(ArticleCategoryResponseDto dto)
    {
        ArticleCategoryCache? existing = await _repo.GetByIdDeletedAsync(dto.Id);
        if (existing is null)
        {
            _logger.LogWarning("SyncRestored: article {Id} not in cache, inserting instead", dto.Id);
            await _repo.AddAsync(ArticleCategoryCache.FromEvent(dto));
        }
        else
        {
            existing.MarkRestored();
            await _repo.UpdateAsync(existing);
        }

        await _repo.SaveChangesAsync();
        _logger.LogInformation("ArticleCache marked restored for {Id}", dto.Id);
    }

    // ── Mapping ───────────────────────────────────────────────────────────────

    private static ArticleCategoryResponseDto MapToDto(ArticleCategoryCache c) => new(
        Id: c.Id,
        Name: c.Name,
        TVA: c.TVA,
        IsDeleted: c.IsDeleted,
        CreatedAt: c.CreatedAt,
        UpdatedAt: c.UpdatedAt, TenantId: c.TenantId);
}