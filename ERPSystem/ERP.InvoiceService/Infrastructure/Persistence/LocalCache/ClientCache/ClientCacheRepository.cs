using ERP.InvoiceService.Application.Interfaces;
using ERP.InvoiceService.Application.Services;
using Microsoft.EntityFrameworkCore;

namespace ERP.InvoiceService.Infrastructure.Persistence.Repositories.LocalCache;

public class ClientCacheRepository : IClientCacheRepository
{
    private readonly InvoiceDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    public ClientCacheRepository(InvoiceDbContext dbContext, ITenantContext tenantContext)
    {
        _tenantContext = tenantContext;
        _dbContext = dbContext;
    }

    public async Task<Domain.LocalCache.Client.ClientCache?> GetByIdAsync(Guid id)
    {
        return await _dbContext.ClientCaches
            .Include(c => c.ClientCategories)
            .ThenInclude(cc => cc.Category)
            .FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task<Domain.LocalCache.Client.ClientCache?> GetByIdDeletedAsync(Guid id)
        => await _dbContext.ClientCaches
            .IgnoreQueryFilters()
            .Include(c => c.ClientCategories)
                .ThenInclude(cc => cc.Category)
            .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == _tenantContext.TenantId);

    public Task DeleteAsync(Domain.LocalCache.Client.ClientCache client)
    {
        if (client is null) throw new ArgumentNullException(nameof(client));
        _dbContext.ClientCaches.Remove(client);
        return Task.CompletedTask;
    }

    public async Task<Domain.LocalCache.Client.ClientCache?> GetByNameAsync(string name)
    {
        return await _dbContext.ClientCaches
            .Include(c => c.ClientCategories)
            .ThenInclude(cc => cc.Category)
            .FirstOrDefaultAsync(c => c.Name == name && !c.IsDeleted);
    }

    public async Task<Domain.LocalCache.Client.ClientCache?> GetByEmailAsync(string email)
        => await _dbContext.ClientCaches
            .Include(c => c.ClientCategories)
                .ThenInclude(cc => cc.Category)
            .FirstOrDefaultAsync(c => c.Email.ToLower() == email.Trim().ToLower());

    public async Task<(List<Domain.LocalCache.Client.ClientCache> Items, int TotalCount)> GetPagedAsync(
        int pageNumber, int pageSize, string? search = null)
    {
        if (pageNumber < 1) pageNumber = 1;
        if (pageSize < 1) pageSize = 10;
        if (pageSize > 100) pageSize = 100;

        IQueryable<Domain.LocalCache.Client.ClientCache> baseQuery = _dbContext.ClientCaches.Where(c=> !c.IsBlocked).AsQueryable().AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            string q = search.Trim().ToLower();
            baseQuery = baseQuery.Where(c =>
                c.Name.ToLower().Contains(q) ||
                c.Email.ToLower().Contains(q)
            );
        }

        int totalCount = await baseQuery.CountAsync(); // counts filtered results

        List<Domain.LocalCache.Client.ClientCache> items = await baseQuery
            .Include(c => c.ClientCategories)
                .ThenInclude(cc => cc.Category)
            .OrderBy(c => c.Name)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<List<Domain.LocalCache.Client.ClientCache>> GetActiveAsync()
        => await _dbContext.ClientCaches
            .Where(c => !c.IsBlocked)
            .Include(c => c.ClientCategories)
                .ThenInclude(cc => cc.Category)
            .OrderBy(c => c.Name)
            .ToListAsync();

    public async Task<bool> ExistsAsync(Guid id)
    {
        return await _dbContext.ClientCaches.AnyAsync(c => c.Id == id);
    }

    public async Task AddAsync(Domain.LocalCache.Client.ClientCache client)
    {
        await _dbContext.ClientCaches.AddAsync(client);
    }

    public Task UpdateAsync(Domain.LocalCache.Client.ClientCache client)
    {
        _dbContext.ClientCaches.Update(client);
        return Task.CompletedTask;
    }

    public async Task SaveChangesAsync()
    {
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear(); // ← stops phantom re-inserts on subsequent operations
    }
}