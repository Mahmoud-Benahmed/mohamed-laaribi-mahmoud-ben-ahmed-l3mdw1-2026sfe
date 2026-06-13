using ERP.ClientService.Application.DTOs;
using ERP.ClientService.Domain;

namespace ERP.ClientService.Application.Interfaces
{
    public interface ICategoryRepository
    {
        // ── Write ─────────────────────────────────────────────────────────────────
        Task AddAsync(Category category);
        Task SaveChangesAsync();

        // ── Single lookups ────────────────────────────────────────────────────────
        Task<Category?> GetByIdAsync(Guid id);
        Task<Category?> GetByIdDeletedAsync(Guid id);
        Task<Category?> GetByCodeAsync(string code);
        Task<bool> DuplicateExists(string code, Guid? excludeId= null);


        // ── Paging & filtering ────────────────────────────────────────────────────

        Task<List<Category>> GetAllAsync();
        Task<(List<Category> Items, int TotalCount)> GetAllPagedAsync(int pageNumber, int pageSize);

        Task<(List<Category> Items, int TotalCount)> GetPagedDeletedAsync(
            int pageNumber, int pageSize);

        Task<(List<Category> Items, int TotalCount)> GetPagedByNameAsync(
            string nameFilter, int pageNumber, int pageSize);

        // ── Stats ─────────────────────────────────────────────────────────────────
        Task<CategoryStatsDto> GetStatsAsync();
    }
}