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
        if (dto == null)
            throw new ArgumentNullException(nameof(dto));

        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            _logger.LogWarning("Category event has null or empty Name. Id: {CategoryId}", dto.Id);
            return;
        }
        _logger.LogWarning($"After SetTenantId, TenantId = {_tenantContext.TenantId}");
        // Try to find by ID first, then by Name
        ArticleCategoryCache? existing = await _repo.GetByIdAsync(dto.Id) ?? await _repo.GetByNameAsync(dto.Name);

        if (existing != null)
        {
            _logger.LogInformation(
                existing.Id == dto.Id
                    ? "Category {Name} (Id: {Id}) found. Updating."
                    : "Category name '{Name}' found with different ID (Existing: {ExistingId}, New: {NewId}). Updating existing.",
                dto.Name, dto.Id, existing.Id);

            existing.ApplyUpdate(dto);
            await _repo.SaveChangesAsync();
            return;
        }

        // Create new category
        _logger.LogInformation("Creating new category: {Name} (Id: {Id})", dto.Name, dto.Id);
        await _repo.AddAsync(ArticleCategoryCache.FromEvent(dto));
        await _repo.SaveChangesAsync();
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