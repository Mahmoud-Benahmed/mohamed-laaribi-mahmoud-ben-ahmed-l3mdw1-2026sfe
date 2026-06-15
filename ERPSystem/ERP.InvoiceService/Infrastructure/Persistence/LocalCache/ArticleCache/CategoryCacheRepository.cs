using ERP.InvoiceService.Application.Interfaces;
using ERP.InvoiceService.Application.Services;
using ERP.InvoiceService.Domain.LocalCache.Article;
using ERP.InvoiceService.Infrastructure.Messaging.Events.TenantEvent;
using Microsoft.EntityFrameworkCore;

namespace ERP.InvoiceService.Infrastructure.Persistence.Repositories.LocalCache.ArticleCache;

public sealed class ArticleCategoryCacheRepository : IArticleCategoryCacheRepository
{
    private readonly InvoiceDbContext _db;
    private readonly ITenantContext _tenantContext;

    public ArticleCategoryCacheRepository(InvoiceDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }
    public async Task<ArticleCategoryCache?> GetByIdAsync(Guid id)
        => await _db.ArticleCategoryCaches
            .FirstOrDefaultAsync(a => a.Id == id);
    public async Task<ArticleCategoryCache?> GetByIdDeletedAsync(Guid id)
        => await _db.ArticleCategoryCaches
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(a => a.Id == id && a.TenantId == _tenantContext.TenantId);

    public async Task<ArticleCategoryCache?> GetByNameAsync(string name)
        => await _db.ArticleCategoryCaches
            .FirstOrDefaultAsync(c => c.Name.ToLower() == name.Trim().ToLower());

    public async Task<List<ArticleCategoryCache>> GetAllAsync()
        => await _db.ArticleCategoryCaches
            .OrderBy(c => c.Name)
            .ToListAsync();

    public async Task<List<ArticleCategoryCache>> GetAllActiveAsync()
        => await _db.ArticleCategoryCaches
            .OrderBy(c => c.Name)
            .ToListAsync();

    public async Task<(List<ArticleCategoryCache> Items, int TotalCount)> GetPagedAsync(
        int pageNumber, int pageSize)
    {
        IQueryable<ArticleCategoryCache> baseQuery = _db.ArticleCategoryCaches
            .AsNoTracking();

        int totalCount = await baseQuery.CountAsync();

        List<ArticleCategoryCache> items = await baseQuery
            .OrderBy(c => c.Name)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public Task DeleteAsync(ArticleCategoryCache category)
    {
        if (category is null) throw new ArgumentNullException(nameof(category));
        _db.ArticleCategoryCaches.Remove(category);
        return Task.CompletedTask;
    }

    public async Task<bool> ExistsAsync(string name)
    {
        return await _db.ArticleCategoryCaches.AnyAsync(c => c.Name == name);
    }
    public Task AddAsync(ArticleCategoryCache article)
    {
        _db.ArticleCategoryCaches.Add(article);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Domain.LocalCache.Article.ArticleCategoryCache article)
    {
        _db.ArticleCategoryCaches.Update(article);
        return Task.CompletedTask;
    }
    public async Task SaveChangesAsync()
        => await _db.SaveChangesAsync();
}