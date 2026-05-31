using ERP.InvoiceService.Domain.LocalCache.Article;
using ERP.InvoiceService.Domain.LocalCache.Client;
using ERP.InvoiceService.Domain.LocalCache.Tenant;

namespace ERP.InvoiceService.Application.Interfaces;

public interface IArticleCacheRepository
{
    Task<List<ArticleCache>> GetByIdsAsync(List<Guid> ids);
    Task<ArticleCache?> GetByIdAsync(Guid id);
    Task<ArticleCache?> GetByIdDeletedAsync(Guid id);
    Task<ArticleCache?> GetByBarCodeAsync(string barCode);
    Task<ArticleCache?> GetByCodeRefAsync(string codeRef);
    Task<List<ArticleCache>> GetAllAsync();
    Task<List<ArticleCache>> GetAllActiveAsync();
    Task<(List<ArticleCache> Items, int TotalCount)> GetPagedAsync(int pageNumber, int pageSize, string? search = null);
    Task AddAsync(ArticleCache article);
    Task SaveChangesAsync();
    Task DeleteAsync(ArticleCache article);
}

public interface IArticleCategoryCacheRepository
{
    Task<bool> ExistsAsync(string name);
    Task<Domain.LocalCache.Article.ArticleCategoryCache?> GetByIdAsync(Guid id);
    Task<Domain.LocalCache.Article.ArticleCategoryCache?> GetByIdDeletedAsync(Guid id);
    Task<Domain.LocalCache.Article.ArticleCategoryCache?> GetByNameAsync(string name);
    Task<List<Domain.LocalCache.Article.ArticleCategoryCache>> GetAllAsync();
    Task<List<Domain.LocalCache.Article.ArticleCategoryCache>> GetAllActiveAsync();
    Task<(List<Domain.LocalCache.Article.ArticleCategoryCache> Items, int TotalCount)> GetPagedAsync(int pageNumber, int pageSize);
    Task AddAsync(Domain.LocalCache.Article.ArticleCategoryCache category);
    Task SaveChangesAsync();
    Task DeleteAsync(Domain.LocalCache.Article.ArticleCategoryCache cache);
}


public interface IClientCacheRepository
{
    Task<ClientCache?> GetByIdAsync(Guid id);
    Task<ClientCache?> GetByIdDeletedAsync(Guid id);
    Task<ClientCache?> GetByNameAsync(string name);
    Task<ClientCache?> GetByEmailAsync(string email);
    Task<(List<ClientCache> Items, int TotalCount)> GetPagedAsync(int pageNumber, int pageSize, string? search = null);
    Task<List<ClientCache>> GetActiveAsync();
    Task<bool> ExistsAsync(Guid id);
    Task AddAsync(ClientCache client);
    Task UpdateAsync(ClientCache client);
    Task SaveChangesAsync();
    Task DeleteAsync(ClientCache client);
}
public interface IClientCategoryCacheRepository
{
    // Read operations - Master data
    Task<Dictionary<Guid, int>> GetClientCountsByCategoryIdsAsync(List<Guid> categoryIds);
    Task<Domain.LocalCache.Client.CategoryCache?> GetByIdAsync(Guid id);
    Task<Domain.LocalCache.Client.CategoryCache?> GetByIdDeletedAsync(Guid id);
    Task<Domain.LocalCache.Client.CategoryCache?> GetByCodeAsync(string code);
    Task<List<Domain.LocalCache.Client.CategoryCache>> GetByClientIdAsync(Guid clientId);
    Task<List<Domain.LocalCache.Client.CategoryCache>> GetByClientNameAsync(string clientName);
    Task<List<Domain.LocalCache.Client.CategoryCache>> GetAllAsync();
    Task<bool> ExistsAsync(Guid id);
    Task<bool> ExistsForClientAsync(Guid clientId, Guid categoryId);
    Task<int> GetCountForClientAsync(Guid clientId);

    // Junction table operations
    Task AssignCategoryToClientAsync(Guid clientId, Guid categoryId);
    Task UnassignCategoryFromClientAsync(Guid clientId, Guid categoryId);
    Task<List<ClientCategoryCache>> GetClientAssignmentsAsync(Guid clientId);

    // Write operations - Master data
    Task AddCategoryAsync(Domain.LocalCache.Client.CategoryCache category);
    Task AddRangeCategoriesAsync(IEnumerable<Domain.LocalCache.Client.CategoryCache> categories);
    Task UpdateCategoryAsync(Domain.LocalCache.Client.CategoryCache category);
    Task DeleteCategoryAsync(Guid id);
    Task DeleteAllCategoriesForClientAsync(Guid clientId);

    // Save changes
    Task SaveChangesAsync();
    Task DeleteAsync(Domain.LocalCache.Client.CategoryCache category);
}

public interface ITenantCacheRepository
{
    Task<TenantCache?> GetByIdAsync(Guid? id);
    Task<TenantCache?> GetBySlugAsync(string slug);
    Task<List<TenantCache>> GetAllAsync();
    Task AddAsync(TenantCache tenant);
    Task SaveChangesAsync();
    Task DeleteAsync(TenantCache tenant);
}