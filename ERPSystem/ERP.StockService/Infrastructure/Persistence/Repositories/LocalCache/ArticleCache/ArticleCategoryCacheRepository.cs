using ERP.StockService.Application.Interfaces;
using ERP.StockService.Domain.LocalCache.Article;
using Microsoft.EntityFrameworkCore;

namespace ERP.StockService.Infrastructure.Persistence.Repositories.LocalCache.ArticleCache;

public sealed class ArticleCategoryCacheRepository : IArticleCategoryCacheRepository
{
    private readonly StockDbContext _db;

    public ArticleCategoryCacheRepository(StockDbContext db) => _db = db;

    public async Task<ArticleCategoryCache?> GetByIdAsync(Guid id)
        => await _db.ArticleCategoryCaches.FindAsync(id);
    public async Task<ArticleCategoryCache?> GetByIdDeletedAsync(Guid id)
    => await _db.ArticleCategoryCaches.IgnoreQueryFilters().FirstOrDefaultAsync(ca=> ca.Id == id);

    public async Task<ArticleCategoryCache?> GetByNameAsync(string name)
        => await _db.ArticleCategoryCaches
            .FirstOrDefaultAsync(c => c.Name == name);

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
        IOrderedQueryable<ArticleCategoryCache> query = _db.ArticleCategoryCaches
            .Where(c => !c.IsDeleted)
            .OrderBy(c => c.Name);

        int totalCount = await query.CountAsync();
        List<ArticleCategoryCache> items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task DeleteAsync(ArticleCategoryCache category)
    {
        if (category == null)
            throw new ArgumentNullException(nameof(category));

        _db.ArticleCategoryCaches.Remove(category); // Use correct DbSet
        await _db.SaveChangesAsync();
    }

    public async Task<bool> ExistsAsync(string name)
    {
        return await _db.ArticleCategoryCaches.AnyAsync(c => c.Name == name);
    }

    public async Task AddAsync(ArticleCategoryCache category)
        => await _db.ArticleCategoryCaches.AddAsync(category);
    public async Task SaveChangesAsync()
        => await _db.SaveChangesAsync();
}