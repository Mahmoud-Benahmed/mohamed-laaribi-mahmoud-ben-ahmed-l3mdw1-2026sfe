using ERP.StockService.Application.Interfaces;
using ERP.StockService.Application.Services;
using ERP.StockService.Domain.LocalCache.Fournisseur;
using Microsoft.EntityFrameworkCore;

namespace ERP.StockService.Infrastructure.Persistence.Repositories.LocalCache;

public class FournisseurCacheRepository : IFournisseurCacheRepository
{
    private readonly StockDbContext _dbContext;
    private readonly ILogger<FournisseurCacheRepository> _logger;
    private readonly ITenantContext _tenantContext;

    public FournisseurCacheRepository(
        StockDbContext dbContext,
        ILogger<FournisseurCacheRepository> logger,
        ITenantContext tenantContext)
    {
        _dbContext = dbContext;
        _logger = logger;
        _tenantContext = tenantContext;
    }

    // =========================
    // READ OPERATIONS
    // =========================

    public async Task<FournisseurCache?> GetByIdAsync(Guid id)
    {

            return await _dbContext.FournisseurCaches
                .FirstOrDefaultAsync(f => f.Id == id);
    }

    public async Task<FournisseurCache?> GetByIdDeletedAsync(Guid id)
        => await _dbContext.FournisseurCaches
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(f => f.Id == id && f.TenantId == _tenantContext.TenantId);

    public async Task<FournisseurCache?> GetByNameAsync(string name)
    {
        return await _dbContext.FournisseurCaches
            .FirstOrDefaultAsync(f => f.Name.ToLower() == name.Trim().ToLower());
    }

    public async Task<FournisseurCache?> GetByTaxNumberAsync(string taxNumber)
    {
            return await _dbContext.FournisseurCaches
                .FirstOrDefaultAsync(f => f.TaxNumber.ToLower() == taxNumber.Trim().ToLower());
    }

    public async Task<FournisseurCache?> GetByEmailAsync(string email)
    {
            return await _dbContext.FournisseurCaches
                .FirstOrDefaultAsync(f => f.Email.ToLower() == email.Trim().ToLower());
    }

    public async Task<List<FournisseurCache>> GetBlockedAsync()
        => await _dbContext.FournisseurCaches
            .Where(f => f.IsBlocked)
            .OrderBy(f => f.Name)
            .ToListAsync();

    public async Task<(List<FournisseurCache> Items, int TotalCount)> GetPagedAsync(
        int pageNumber, int pageSize, string? search = null)
    {
        if (pageNumber < 1) pageNumber = 1;
        if (pageSize < 1) pageSize = 10;
        if (pageSize > 100) pageSize = 100;

        IQueryable<FournisseurCache> baseQuery = _dbContext.FournisseurCaches.AsQueryable().AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            string q = search.Trim().ToLower();
            baseQuery = baseQuery.Where(c =>
                c.Name.ToLower().Contains(q) ||
                (c.Email != null && c.Email.ToLower().Contains(q))
            );
        }
        int totalCount = await baseQuery.CountAsync();

        List<FournisseurCache> items = await baseQuery
            .OrderBy(c => c.Name)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<bool> ExistsAsync(Guid id)
    {
        try
        {
            return await _dbContext.FournisseurCaches.AnyAsync(f => f.Id == id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking existence for fournisseur ID {FournisseurId}", id);
            throw;
        }
    }

    public async Task<bool> ExistsByNameAsync(string name)
    {
        try
        {
            return await _dbContext.FournisseurCaches.AnyAsync(f => f.Name == name && !f.IsDeleted);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking existence for fournisseur name {FournisseurName}", name);
            throw;
        }
    }

    public async Task<bool> ExistsByTaxNumberAsync(string taxNumber)
    {
        try
        {
            return await _dbContext.FournisseurCaches.AnyAsync(f => f.TaxNumber == taxNumber && !f.IsDeleted);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking existence for tax number {TaxNumber}", taxNumber);
            throw;
        }
    }

    public async Task<int> GetCountAsync()
    {
        return await _dbContext.FournisseurCaches.CountAsync();
    }

    // =========================
    // WRITE OPERATIONS
    // =========================

    public Task AddAsync(FournisseurCache fournisseur)
    {
        if (fournisseur is null) throw new ArgumentNullException(nameof(fournisseur));
        _dbContext.FournisseurCaches.Add(fournisseur);
        return Task.CompletedTask;
    }

    public Task AddRangeAsync(IEnumerable<FournisseurCache> fournisseurs)
    {

        if (fournisseurs == null)
            throw new ArgumentNullException(nameof(fournisseurs));

        List<FournisseurCache> fournisseurList = fournisseurs.ToList();
        if (!fournisseurList.Any())
            return Task.CompletedTask;

        _dbContext.FournisseurCaches.AddRangeAsync(fournisseurList);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(FournisseurCache fournisseur)
    {
            if (fournisseur == null)
                throw new ArgumentNullException(nameof(fournisseur));

            _dbContext.FournisseurCaches.Update(fournisseur);
            return Task.CompletedTask;
    }

    public Task DeleteAsync(FournisseurCache fournisseur)
    {

        if (fournisseur == null)
            throw new ArgumentNullException(nameof(fournisseur));

        _dbContext.FournisseurCaches.Remove(fournisseur);
        return Task.CompletedTask;
    }

    public async Task DeletePermanentlyAsync(Guid id)
    {
        try
        {
            FournisseurCache? fournisseur = await GetByIdAsync(id);
            if (fournisseur != null)
            {
                _dbContext.FournisseurCaches.Remove(fournisseur);
                _logger.LogDebug("Fournisseur {FournisseurId} permanently deleted", id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error permanently deleting fournisseur {FournisseurId}", id);
            throw;
        }
    }

    public async Task SaveChangesAsync()
    {
        try
        {
            await _dbContext.SaveChangesAsync();
            _logger.LogDebug("Fournisseur changes saved to database");
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Database error while saving fournisseur changes");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving fournisseur changes");
            throw;
        }
    }
}