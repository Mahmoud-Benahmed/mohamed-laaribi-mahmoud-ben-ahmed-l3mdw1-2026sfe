using ERP.InvoiceService.Application.Interfaces;
using ERP.InvoiceService.Application.Services;
using Microsoft.EntityFrameworkCore;

namespace ERP.InvoiceService.Infrastructure.Persistence.Repositories.LocalCache;

public sealed class ArticleCacheRepository : IArticleCacheRepository
{
    private readonly InvoiceDbContext _db;
    private readonly ITenantContext _tenantContext;
    public ArticleCacheRepository(InvoiceDbContext db, ITenantContext tenantContext)
    {
        _tenantContext = tenantContext;
        _db = db;
    }

    public async Task<List<Domain.LocalCache.Article.ArticleCache>> GetByIdsAsync(List<Guid> ids)
        => await _db.ArticleCaches
            .Include(a => a.Category)
            .Where(a => ids.Contains(a.Id))
            .ToListAsync();

    public async Task<Domain.LocalCache.Article.ArticleCache?> GetByIdAsync(Guid id)
    => await _db.ArticleCaches
        .Include(a => a.Category)
        .FirstOrDefaultAsync(a => a.Id == id);

    public async Task<Domain.LocalCache.Article.ArticleCache?> GetByIdDeletedAsync(Guid id)
        => await _db.ArticleCaches
            .IgnoreQueryFilters()
            .Include(a => a.Category)
            .FirstOrDefaultAsync(a => a.Id == id && a.TenantId == _tenantContext.TenantId);

    public async Task<Domain.LocalCache.Article.ArticleCache?> GetByBarCodeAsync(string barCode)
        => await _db.ArticleCaches
            .Include(a => a.Category)
            .FirstOrDefaultAsync(a => a.BarCode == barCode);

    public async Task<Domain.LocalCache.Article.ArticleCache?> GetByCodeRefAsync(string codeRef)
        => await _db.ArticleCaches
            .Include(a => a.Category)
            .FirstOrDefaultAsync(a => a.CodeRef == codeRef);

    public async Task<List<Domain.LocalCache.Article.ArticleCache>> GetAllAsync()
        => await _db.ArticleCaches
            .Include(a => a.Category)
            .OrderBy(a => a.Libelle)
            .ToListAsync();

    public async Task<List<Domain.LocalCache.Article.ArticleCache>> GetAllActiveAsync()
        => await _db.ArticleCaches
            .Include(a => a.Category)
            .Where(a => !a.IsDeleted)   // or whatever your "active" flag is
            .OrderBy(a => a.Libelle)
            .ToListAsync();

    public async Task<(List<Domain.LocalCache.Article.ArticleCache> Items, int TotalCount)> GetPagedAsync(
        int pageNumber, int pageSize, string? search = null)
    {
        if (pageNumber < 1) pageNumber = 1;
        if (pageSize < 1) pageSize = 10;
        if (pageSize > 100) pageSize = 100;

        IQueryable<Domain.LocalCache.Article.ArticleCache> baseQuery = _db.ArticleCaches.AsQueryable().AsNoTracking(); ;

        if (!string.IsNullOrWhiteSpace(search))
        {
            string q = search.Trim().ToLower();
            baseQuery = baseQuery.Where(c =>
                (c.BarCode != null && c.BarCode.ToLower().Contains(q)) ||
                c.Libelle.ToLower().Contains(q) ||
                c.CodeRef.ToLower().Contains(q)
            );
        }


        int totalCount = await baseQuery.CountAsync();

        List<Domain.LocalCache.Article.ArticleCache> items = await baseQuery
            .Include(a => a.Category)   // ← move up
            .OrderBy(a => a.Libelle)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public Task AddAsync(Domain.LocalCache.Article.ArticleCache article)
    {
        _db.ArticleCaches.Add(article);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Domain.LocalCache.Article.ArticleCache article)
    {
        _db.ArticleCaches.Update(article);
        return Task.CompletedTask;
    }

    public async Task SaveChangesAsync()
        => await _db.SaveChangesAsync();

    public Task DeleteAsync(Domain.LocalCache.Article.ArticleCache article)
    {
        if (article is null) throw new ArgumentNullException(nameof(article));
        _db.ArticleCaches.Remove(article);
        return Task.CompletedTask; // caller calls SaveChangesAsync
    }
}