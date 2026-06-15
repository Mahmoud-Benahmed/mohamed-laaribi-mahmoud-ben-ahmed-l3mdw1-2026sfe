using ERP.AuthService.Domain;

namespace ERP.AuthService.Application.Interfaces.Repositories
{
    public interface IRoleRepository
    {
        Task<Role?> GetByIdAsync(Guid id);
        Task<Role?> GetByLibelleAsync(string libelle);
        Task<bool> DuplicateExists(string libelle, Guid? excludeId = null);
        Task<(List<Role> Items, int TotalCount)> GetAllPagedAsync(int pageNumber, int pageSize);
        Task<List<Role>> GetAllAsync();

        Task AddAsync(Role role);
        Task UpdateAsync(Role role);
        Task DeleteAsync(Guid id);
        Task DeleteAllAsync();
        Task<long> CountAsync();
    }
}
