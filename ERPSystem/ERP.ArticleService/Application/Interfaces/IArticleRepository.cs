using ERP.ArticleService.Application.DTOs;
using ERP.ArticleService.Domain;
namespace ERP.ArticleService.Application.Interfaces
{

    public interface IArticleRepository
    {
        Task AddAsync(Article article);
        Task<Article?> GetByIdAsync(Guid id);
        Task<Article?> GetByIdDeletedAsync(Guid id);
        Task<Article?> GetByCodeAsync(string code);
        Task<Article?> GetByBarCodeAsync(string barCode);

        Task<bool> ExistsForCategoryAsync(Guid categoryId);
        Task<bool> ExistsByIdAsync(Guid id);

        Task SaveChangesAsync();

        // Paging & filtering
        Task<(List<Article> Items, int TotalCount)> GetAllAsync(int pageNumber, int pageSize);
        Task<(List<Article> Items, int TotalCount)> GetPagedByCategoryIdAsync(Guid categoryId, int pageNumber, int pageSize);
        Task<(List<Article> Items, int TotalCount)> GetPagedByLibelleAsync(string libelleFilter, int pageNumber, int pageSize);
        Task<(List<Article> Items, int TotalCount)> GetPagedDeletedAsync(int pageNumber, int pageSize);

        Task<ArticleStatsDto> GetStatsAsync();
    }
}
