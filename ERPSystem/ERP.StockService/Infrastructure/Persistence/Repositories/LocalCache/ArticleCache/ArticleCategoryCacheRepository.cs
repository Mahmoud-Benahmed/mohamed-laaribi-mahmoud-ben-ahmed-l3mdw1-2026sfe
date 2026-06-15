using ERP.StockService.Application.Interfaces;
using ERP.StockService.Application.Services;
using ERP.StockService.Domain.LocalCache.Article;
using Microsoft.EntityFrameworkCore;

namespace ERP.StockService.Infrastructure.Persistence.Repositories.LocalCache.ArticleCache;

public sealed class ArticleCategoryCacheRepository : IArticleCategoryCacheRepository
{
    private readonly StockDbContext _db;
    private readonly ITenantContext _tenantContext;

    public ArticleCategoryCacheRepository(StockDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<ArticleCategoryCache?> GetByIdAsync(Guid id)
        => await _db.ArticleCategoryCaches.FirstOrDefaultAsync(c => c.Id == id);

    public async Task<ArticleCategoryCache?> GetByIdDeletedAsync(Guid id)
        => await _db.ArticleCategoryCaches
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(ca => ca.Id == id && ca.TenantId == _tenantContext.TenantId);

    public async Task<List<ArticleCategoryCache>> GetAllAsync()
        => await _db.ArticleCategoryCaches
            .OrderBy(c => c.Name)
            .ToListAsync();

    public async Task<List<ArticleCategoryCache>> GetAllActiveAsync()
        => await _db.ArticleCategoryCaches
            .Where(c => !c.IsDeleted)
            .OrderBy(c => c.Name)
            .ToListAsync();

    public async Task<(List<ArticleCategoryCache> Items, int TotalCount)> GetPagedAsync(
        int pageNumber, int pageSize)
    {
        IQueryable<ArticleCategoryCache> baseQuery = _db.ArticleCategoryCaches.AsNoTracking();

        int totalCount = await baseQuery.CountAsync();

        List<ArticleCategoryCache> items = await baseQuery
            .OrderBy(c => c.Name)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<ArticleCategoryCache?> GetByNameAsync(string name)
        => await _db.ArticleCategoryCaches
            .FirstOrDefaultAsync(c => c.Name.ToLower() == name.Trim().ToLower());

    public async Task<bool> ExistsAsync(string name)
        => await _db.ArticleCategoryCaches
            .AnyAsync(c => c.Name.ToLower() == name.Trim().ToLower());

    public Task AddAsync(ArticleCategoryCache category)
    {
        _db.ArticleCategoryCaches.Add(category);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(ArticleCategoryCache category)
    {
        if (category is null) throw new ArgumentNullException(nameof(category));
        _db.ArticleCategoryCaches.Remove(category);
        return Task.CompletedTask;
    }
    public async Task SaveChangesAsync()
        => await _db.SaveChangesAsync();
}