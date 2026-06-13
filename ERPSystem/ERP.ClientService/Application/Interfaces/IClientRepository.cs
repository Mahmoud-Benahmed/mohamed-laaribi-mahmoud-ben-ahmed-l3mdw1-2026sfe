using ERP.ClientService.Application.DTOs;
using ERP.ClientService.Domain;

namespace ERP.ClientService.Application.Interfaces
{
    public interface IClientRepository
    {
        // ── Write ─────────────────────────────────────────────────────────────────
        Task AddAsync(Client client);
        Task SaveChangesAsync();

        // ── Single lookups ────────────────────────────────────────────────────────
        Task<Client?> GetByIdAsync(Guid id);
        Task<Client?> GetByIdDeletedAsync(Guid id);   // bypasses IsDeleted filter
        Task<Client?> GetByEmailAsync(string email);
        Task<bool> DuplicateExists(string email, string phone, Guid? excludeId= null);
        // ── Paging & filtering ────────────────────────────────────────────────────
        Task<(List<Client> Items, int TotalCount)> GetAllAsync(
            int pageNumber, int pageSize);

        Task<(List<Client> Items, int TotalCount)> GetPagedByCategoryIdAsync(
            Guid categoryId, int pageNumber, int pageSize);

        Task<(List<Client> Items, int TotalCount)> GetPagedDeletedAsync(
            int pageNumber, int pageSize);

        Task<(List<Client> Items, int TotalCount)> GetPagedByNameAsync(
            string nameFilter, int pageNumber, int pageSize);

        // ── Stats ─────────────────────────────────────────────────────────────────
        Task<ClientStatsDto> GetStatsAsync();
    }
}