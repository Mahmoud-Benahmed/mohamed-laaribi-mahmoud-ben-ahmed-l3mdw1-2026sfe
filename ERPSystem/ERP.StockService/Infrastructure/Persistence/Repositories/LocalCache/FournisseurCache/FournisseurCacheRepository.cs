using ERP.StockService.Application.Interfaces;
using ERP.StockService.Domain.LocalCache.Fournisseur;
using Microsoft.EntityFrameworkCore;

namespace ERP.StockService.Infrastructure.Persistence.Repositories.LocalCache;

public class FournisseurCacheRepository : IFournisseurCacheRepository
{
    private readonly StockDbContext _dbContext;
    private readonly ILogger<FournisseurCacheRepository> _logger;

    public FournisseurCacheRepository(
        StockDbContext dbContext,
        ILogger<FournisseurCacheRepository> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    // =========================
    // READ OPERATIONS
    // =========================

    public async Task<FournisseurCache?> GetByIdAsync(Guid id)
    {
        try
        {
            return await _dbContext.FournisseurCaches
                .FirstOrDefaultAsync(f => f.Id == id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting fournisseur by ID {FournisseurId}", id);
            throw;
        }
    }

    public async Task<FournisseurCache?> GetByIdDeletedAsync(Guid id)
    {
        try
        {
            return await _dbContext.FournisseurCaches.IgnoreQueryFilters()
                .FirstOrDefaultAsync(f => f.Id == id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting fournisseur by ID {FournisseurId}", id);
            throw;
        }
    }

    public async Task<FournisseurCache?> GetByNameAsync(string name)
    {
        try
        {
            return await _dbContext.FournisseurCaches
                .FirstOrDefaultAsync(f => f.Name == name && !f.IsDeleted);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting fournisseur by name {FournisseurName}", name);
            throw;
        }
    }

    public async Task<FournisseurCache?> GetByTaxNumberAsync(string taxNumber)
    {
        try
        {
            return await _dbContext.FournisseurCaches
                .FirstOrDefaultAsync(f => f.TaxNumber == taxNumber && !f.IsDeleted);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting fournisseur by tax number {TaxNumber}", taxNumber);
            throw;
        }
    }

    public async Task<FournisseurCache?> GetByEmailAsync(string email)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(email))
                return null;

            return await _dbContext.FournisseurCaches
                .FirstOrDefaultAsync(f => f.Email == email && !f.IsDeleted);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting fournisseur by email {Email}", email);
            throw;
        }
    }

    public async Task<List<FournisseurCache>> GetBlockedAsync()
    {
        try
        {
            return await _dbContext.FournisseurCaches
                .Where(f => !f.IsDeleted && f.IsBlocked)
                .OrderBy(f => f.Name)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting blocked fournisseurs");
            throw;
        }
    }

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
                c.Email.ToLower().Contains(q)
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
        try
        {
            return await _dbContext.FournisseurCaches.CountAsync(f => !f.IsDeleted);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting fournisseur count");
            throw;
        }
    }

    // =========================
    // WRITE OPERATIONS
    // =========================

    public async Task AddAsync(FournisseurCache fournisseur)
    {
        try
        {
            if (fournisseur == null)
                throw new ArgumentNullException(nameof(fournisseur));

            await _dbContext.FournisseurCaches.AddAsync(fournisseur);
            _logger.LogDebug("Fournisseur {FournisseurName} added to context", fournisseur.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding fournisseur {FournisseurName}", fournisseur.Name);
            throw;
        }
    }

    public async Task AddRangeAsync(IEnumerable<FournisseurCache> fournisseurs)
    {
        try
        {
            if (fournisseurs == null)
                throw new ArgumentNullException(nameof(fournisseurs));

            List<FournisseurCache> fournisseurList = fournisseurs.ToList();
            if (!fournisseurList.Any())
                return;

            await _dbContext.FournisseurCaches.AddRangeAsync(fournisseurList);
            _logger.LogDebug("Added {Count} fournisseurs to context", fournisseurList.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding range of fournisseurs");
            throw;
        }
    }

    public Task UpdateAsync(FournisseurCache fournisseur)
    {
        try
        {
            if (fournisseur == null)
                throw new ArgumentNullException(nameof(fournisseur));

            _dbContext.FournisseurCaches.Update(fournisseur);
            _logger.LogDebug("Fournisseur {FournisseurName} marked as updated", fournisseur.Name);
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating fournisseur {FournisseurName}", fournisseur.Name);
            throw;
        }
    }

    public Task DeleteAsync(FournisseurCache fournisseur)
    {
        try
        {
            if (fournisseur == null)
                throw new ArgumentNullException(nameof(fournisseur));

            fournisseur.MarkDeleted();
            _dbContext.FournisseurCaches.Update(fournisseur);
            _logger.LogDebug("Fournisseur {FournisseurName} marked as deleted (soft delete)", fournisseur.Name);
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting fournisseur {FournisseurName}", fournisseur.Name);
            throw;
        }
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