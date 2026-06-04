// Infrastructure/Persistence/Repositories/LocalCache/ClientCategoryCacheRepository.cs
using ERP.StockService.Application.Interfaces;
using ERP.StockService.Domain.LocalCache.Client;
using Microsoft.EntityFrameworkCore;

namespace ERP.StockService.Infrastructure.Persistence.Repositories.LocalCache.ClientCache;

public class ClientCategoryCacheRepository : IClientCategoryCacheRepository
{
    private readonly StockDbContext _dbContext;
    private readonly ILogger<ClientCategoryCacheRepository> _logger;

    public ClientCategoryCacheRepository(
        StockDbContext dbContext,
        ILogger<ClientCategoryCacheRepository> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }


    // =========================
    // READ OPERATIONS - Master Data
    // =========================

    public async Task<Domain.LocalCache.Client.CategoryCache?> GetByIdAsync(Guid id)
    {
        try
        {
            return await _dbContext.ClientCategoryMasterCaches
                .FirstOrDefaultAsync(cc => cc.Id == id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting client category by ID {CategoryId}", id);
            throw;
        }
    }

    public async Task<Domain.LocalCache.Client.CategoryCache?> GetByIdDeletedAsync(Guid id)
    {
        try
        {
            return await _dbContext.ClientCategoryMasterCaches.IgnoreQueryFilters()
                .FirstOrDefaultAsync(cc => cc.Id == id && cc.IsDeleted);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting client category by ID {CategoryId}", id);
            throw;
        }
    }

    public async Task DeleteAsync(CategoryCache category)
    {
        category.Delete();
        _dbContext.ClientCategoryMasterCaches.Update(category);
        await _dbContext.SaveChangesAsync();
    }

    public async Task<Dictionary<Guid, int>> GetClientCountsByCategoryIdsAsync(List<Guid> categoryIds)
    {
        return await _dbContext.ClientCategoryAssignments
            .Where(cca => categoryIds.Contains(cca.CategoryId))
            .Join(_dbContext.ClientCaches,
                  cca => cca.ClientId,
                  c => c.Id,
                  (cca, c) => new { cca.CategoryId })
            .GroupBy(x => x.CategoryId)
            .Select(g => new { CategoryId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.CategoryId, x => x.Count);
    }

    public async Task<Domain.LocalCache.Client.CategoryCache?> GetByCodeAsync(string code)
    {
        try
        {
            return await _dbContext.ClientCategoryMasterCaches
                .FirstOrDefaultAsync(cc => cc.Code == code);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting client category by code {CategoryCode}", code);
            throw;
        }
    }

    public async Task<List<Domain.LocalCache.Client.CategoryCache>> GetByClientIdAsync(Guid clientId)
    {
        try
        {
            // Get category IDs from junction table, then fetch master data
            List<Guid> categoryIds = await _dbContext.ClientCategoryAssignments
                .Where(ca => ca.ClientId == clientId)
                .Select(ca => ca.CategoryId)
                .ToListAsync();

            return await _dbContext.ClientCategoryMasterCaches
                .Where(c => categoryIds.Contains(c.Id))
                .OrderBy(c => c.Name)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting categories for client {ClientId}", clientId);
            throw;
        }
    }

    public async Task<List<Domain.LocalCache.Client.CategoryCache>> GetByClientNameAsync(string clientName)
    {
        try
        {
            // Then get categories for that client
            return await _dbContext.ClientCategoryAssignments
                    .Where(ca => ca.Client.Name == clientName)
                    .Select(ca => ca.Category)
                    .OrderBy(c => c.Name)
                    .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting categories for client name {ClientName}", clientName);
            throw;
        }
    }

    public async Task<List<Domain.LocalCache.Client.CategoryCache>> GetAllAsync()
    {
        try
        {
            return await _dbContext.ClientCategoryMasterCaches
                .OrderBy(c => c.Name)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all client categories");
            throw;
        }
    }

    public async Task<bool> ExistsAsync(Guid id)
    {
        try
        {
            return await _dbContext.ClientCategoryMasterCaches
                .AnyAsync(cc => cc.Id == id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking existence for category ID {CategoryId}", id);
            throw;
        }
    }

    public async Task<bool> ExistsForClientAsync(Guid clientId, Guid categoryId)
    {
        try
        {
            return await _dbContext.ClientCategoryAssignments
                .AnyAsync(ca => ca.ClientId == clientId && ca.CategoryId == categoryId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking category assignment for client {ClientId}, category {CategoryId}",
                clientId, categoryId);
            throw;
        }
    }

    public async Task<int> GetCountForClientAsync(Guid clientId)
    {
        try
        {
            return await _dbContext.ClientCategoryAssignments
                .CountAsync(ca => ca.ClientId == clientId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting category count for client {ClientId}", clientId);
            throw;
        }
    }

    // =========================
    // JUNCTION TABLE OPERATIONS (Assignments)
    // =========================

    public async Task AssignCategoryToClientAsync(Guid clientId, Guid categoryId)
    {
        try
        {
            ClientCategoryCache assignment = ClientCategoryCache.Create(clientId, categoryId);
            await _dbContext.ClientCategoryAssignments.AddAsync(assignment);
            _logger.LogDebug("Category {CategoryId} assigned to client {ClientId}",
                categoryId, clientId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assigning category {CategoryId} to client {ClientId}",
                categoryId, clientId);
            throw;
        }
    }

    public async Task UnassignCategoryFromClientAsync(Guid clientId, Guid categoryId)
    {
        try
        {
            ClientCategoryCache? assignment = await _dbContext.ClientCategoryAssignments
                .FirstOrDefaultAsync(ca => ca.ClientId == clientId && ca.CategoryId == categoryId);

            if (assignment != null)
            {
                _dbContext.ClientCategoryAssignments.Remove(assignment);
                _logger.LogDebug("Category {CategoryId} unassigned from client {ClientId}",
                    categoryId, clientId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unassigning category {CategoryId} from client {ClientId}",
                categoryId, clientId);
            throw;
        }
    }

    public async Task<List<ClientCategoryCache>> GetClientAssignmentsAsync(Guid clientId)
    {
        try
        {
            return await _dbContext.ClientCategoryAssignments
                .Include(ca => ca.Category)
                .Where(ca => ca.ClientId == clientId)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting assignments for client {ClientId}", clientId);
            throw;
        }
    }

    // =========================
    // WRITE OPERATIONS - Master Data
    // =========================

    public async Task AddCategoryAsync(Domain.LocalCache.Client.CategoryCache category)
    {
        try
        {
            if (category == null)
                throw new ArgumentNullException(nameof(category));

            await _dbContext.ClientCategoryMasterCaches.AddAsync(category);
            _logger.LogDebug("Client category {CategoryName} added to master data", category.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding client category {CategoryName}", category.Name);
            throw;
        }
    }

    public async Task AddRangeCategoriesAsync(IEnumerable<Domain.LocalCache.Client.CategoryCache> categories)
    {
        try
        {
            if (categories == null)
                throw new ArgumentNullException(nameof(categories));

            List<CategoryCache> categoryList = categories.ToList();
            if (!categoryList.Any())
                return;

            await _dbContext.ClientCategoryMasterCaches.AddRangeAsync(categoryList);
            _logger.LogDebug("Added {Count} client categories to master data", categoryList.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding range of client categories");
            throw;
        }
    }

    public Task UpdateCategoryAsync(Domain.LocalCache.Client.CategoryCache category)
    {
        try
        {
            if (category == null)
                throw new ArgumentNullException(nameof(category));

            _dbContext.ClientCategoryMasterCaches.Update(category);
            _logger.LogDebug("Client category {CategoryName} marked as updated", category.Name);
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating client category {CategoryName}", category.Name);
            throw;
        }
    }

    public async Task DeleteCategoryAsync(Guid id)
    {
        try
        {
            CategoryCache? category = await GetByIdAsync(id);
            if (category != null)
            {
                category.Delete();
                _dbContext.ClientCategoryMasterCaches.Update(category);
                _logger.LogDebug("Client category {CategoryId} soft deleted", id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting client category {CategoryId}", id);
            throw;
        }
    }

    public async Task DeleteAllCategoriesForClientAsync(Guid clientId)
    {
        try
        {
            List<ClientCategoryCache> assignments = await _dbContext.ClientCategoryAssignments
                .Where(ca => ca.ClientId == clientId)
                .ToListAsync();

            if (assignments.Any())
            {
                _dbContext.ClientCategoryAssignments.RemoveRange(assignments);
                _logger.LogDebug("Removed {Count} category assignments for client {ClientId}",
                    assignments.Count, clientId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting categories for client {ClientId}", clientId);
            throw;
        }
    }

    public async Task SaveChangesAsync()
    {
        try
        {
            await _dbContext.SaveChangesAsync();
            _dbContext.ChangeTracker.Clear(); // ← add this
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Database error while saving client category changes");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving client category changes");
            throw;
        }
    }
}