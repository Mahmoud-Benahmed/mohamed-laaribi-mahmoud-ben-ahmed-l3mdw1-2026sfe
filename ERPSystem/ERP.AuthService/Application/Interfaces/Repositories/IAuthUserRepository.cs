using ERP.AuthService.Application.DTOs.AuthUser;
using ERP.AuthService.Domain;

namespace ERP.AuthService.Application.Interfaces.Repositories
{
    public interface IAuthUserRepository
    {
        Task AddAsync(AuthUser user);
        Task<AuthUser?> GetByLoginAsync(string login);
        Task<AuthUser?> GetByEmailAsync(string email);
        Task<AuthUser?> GetByIdAsync(Guid id);
        Task<AuthUser?> GetByDeletedIdAsync(Guid id);

        Task<(List<AuthUser> Items, int TotalCount)> GetAllAsync(int pageNumber, int pageSize, Guid? excludeId = null);
        Task<(List<AuthUser> Items, int TotalCount)> GetPagedByStatusAsync(bool status, int pageNumber, int pageSize, Guid? excludeId = null);
        Task<(List<AuthUser> Items, int TotalCount)> GetPagedByRoleAsync(Guid role, int pageNumber, int pageSize, Guid? excludeId = null);
        Task<(List<AuthUser> Items, int TotalCount)> GetDeletedPagedAsync(int pageNumber, int pageSize, Guid? excludeId = null);

        Task<int> CountByTenantIdAsync(Guid tenantId);
        Task<AuthUser?> UpdateAsync(AuthUser user);
        Task<bool> ExistsByLoginAsync(string login);
        Task<bool> ExistsByEmailAsync(string email);
        Task<int> CountActiveAsync();
        Task<int> CountByStatusAsync(bool status);
        Task<int> CountByRoleIdAsync(Guid roleId);
        Task<UserStatsDto> GetStatsAsync(Guid? excludeId = default);

        Task DeleteAllAsync();

    }
}
