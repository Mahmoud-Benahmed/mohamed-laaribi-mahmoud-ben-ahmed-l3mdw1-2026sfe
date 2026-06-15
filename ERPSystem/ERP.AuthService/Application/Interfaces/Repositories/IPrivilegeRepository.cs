using ERP.AuthService.Domain;

namespace ERP.AuthService.Application.Interfaces.Repositories
{
    public interface IPrivilegeRepository
    {
        Task<Privilege?> GetByIdAsync(Guid id);
        Task<Privilege?> GetByRoleIdAndControleIdAsync(Guid roleId, Guid controleId);
        Task<List<Privilege>> GetByRoleIdAsync(Guid roleId);
        Task<List<Privilege>> GetByControleIdAsync(Guid controleId);
        Task AddAsync(Privilege privilege);
        Task UpdateAsync(Privilege privilege);
        Task DeleteAsync(Guid id);
        Task DeleteByControleIdAsync(Guid controleId);
        Task DeleteByRoleIdAsync(Guid roleId);
        Task DeleteAllAsync();
        Task<long> CountAsync();
    }
}
