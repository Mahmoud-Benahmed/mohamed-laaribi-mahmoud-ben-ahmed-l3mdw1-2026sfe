using ERP.StockService.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.StockService.Infrastructure.Persistence.Repositories;

public class JournalStockRepository : IJournalStockRepository
{
    private readonly StockDbContext _context;
    public JournalStockRepository(StockDbContext context) => _context = context;

    public async Task AddAsync(JournalStock entry)
        => await _context.JournalStocks.AddAsync(entry);

    public async Task SaveChangesAsync() => await _context.SaveChangesAsync();
    public async Task<List<JournalStock>> GetByArticleAsync(Guid articleId)
        => await _context.JournalStocks.Where(js => js.ArticleId == articleId).ToListAsync();

    public async Task DeleteAsync(JournalStock entry)
    {
        _context.JournalStocks.Remove(entry);
        await _context.SaveChangesAsync();
    }

    // In your JournalStockRepository
    public async Task<decimal> GetCurrentStockAsync(Guid articleId)
    {
        // Returns 0 if no movements exist yet for this article
        List<JournalStock> movements = await _context.JournalStocks
            .Where(j => j.ArticleId == articleId)
            .ToListAsync();

        return movements.Any() ? movements.Sum(j => j.Quantity) : 0m;
    }

    public async Task<Dictionary<string, List<StockItem>>> GetArticlesWithStockAsync()
    {
        var stockData = await _context.JournalStocks
            .Distinct()
            .GroupBy(j => j.ArticleId)
            .Select(g => new
            {
                ArticleId = g.Key,
                TotalStock = g.Sum(j => j.Quantity)
            })
            .Where(s => s.TotalStock > 0)
            .ToListAsync();

        Dictionary<string, List<StockItem>> result = new Dictionary<string, List<StockItem>>
        {
            ["IN_STOCK"] = stockData
                .Where(s => s.TotalStock > 0)
                .Select(s => new StockItem { ArticleId = s.ArticleId, Quantity = s.TotalStock })
                .ToList(),

            ["OUT_STOCK"] = stockData
                .Where(s => s.TotalStock < 0)
                .Select(s => new StockItem { ArticleId = s.ArticleId, Quantity = Math.Abs(s.TotalStock) })
                .ToList()
        };

        return result;
    }

    public async Task<Dictionary<Guid, decimal>> GetCurrentStocksAsync(IEnumerable<Guid> articleIds)
    {
        List<Guid> ids = articleIds.ToList();

        return await _context.JournalStocks
            .Where(j => ids.Contains(j.ArticleId))
            .GroupBy(j => j.ArticleId)
            .Select(g => new
            {
                ArticleId = g.Key,
                Stock = g.OrderByDescending(j => j.CreatedAt).First().StockAfter
            })
            .ToDictionaryAsync(x => x.ArticleId, x => x.Stock);
    }
}


public class StockStatusResponse
{
    public List<StockItem> IN_STOCK { get; set; } = new();
    public List<StockItem> OUT_STOCK { get; set; } = new();
}

public class StockItem
{
    public Guid ArticleId { get; set; }
    public decimal Quantity { get; set; }
}