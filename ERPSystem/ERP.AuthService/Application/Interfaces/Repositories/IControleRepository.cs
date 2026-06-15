using ERP.AuthService.Domain;

namespace ERP.AuthService.Application.Interfaces.Repositories
{
    public interface IControleRepository
    {
        Task<bool> DuplicateExists(string libelle, Guid? excludeId = null);
        Task<Controle?> GetByIdAsync(Guid id);
        Task<Controle?> GetByLibelleAsync(string libelle);
        Task<(List<Controle> Items, int TotalCount)> GetAllPagedAsync(int pageNumber, int pageSize);
        Task<List<Controle>> GetAllAsync();
        Task<(List<Controle> Items, int TotalCount)> GetByCategoryAsync(string category, int pageNum, int pageSize);
        Task AddAsync(Controle controle);
        Task UpdateAsync(Controle controle);
        Task DeleteAsync(Guid id);
        Task DeleteAllAsync();
        Task<int> CountAsync();
        Task<List<Controle>> GetByIdsAsync(IEnumerable<Guid> ids);
    }
}
