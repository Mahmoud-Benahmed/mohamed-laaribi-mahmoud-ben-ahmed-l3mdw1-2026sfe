using ERP.AuthService.Application.DTOs.Role;
using ERP.AuthService.Application.Exceptions;
using ERP.AuthService.Application.Interfaces.Repositories;
using ERP.AuthService.Application.Interfaces.Services;
using ERP.AuthService.Domain;

namespace ERP.AuthService.Application.Services
{
    public class PrivilegeService : IPrivilegeService
    {
        private readonly IPrivilegeRepository _privilegeRepository;
        private readonly IControleRepository _controleRepository;
        private readonly IRoleRepository _roleRepository;
        private readonly ITenantContext _tenantContext;

        public PrivilegeService(
            IPrivilegeRepository privilegeRepository,
            IControleRepository controleRepository,
            IRoleRepository roleRepository,
            ITenantContext tenantContext)
        {
            _privilegeRepository = privilegeRepository;
            _controleRepository = controleRepository;
            _roleRepository = roleRepository;
            _tenantContext = tenantContext;
        }

        public async Task<List<PrivilegeResponseDto>> GetByRoleIdAsync(Guid roleId)
        {
            _ = await _roleRepository.GetByIdAsync(roleId)
                ?? throw new RoleNotFoundException(roleId);

            List<Privilege> privileges = await _privilegeRepository.GetByRoleIdAsync(roleId);
            List<PrivilegeResponseDto> result = new List<PrivilegeResponseDto>();

            foreach (Privilege p in privileges)
            {
                Controle controle = await _controleRepository.GetByIdAsync(p.ControleId)
                               ?? throw new ControleNotFoundException(p.ControleId);

                result.Add(new PrivilegeResponseDto(
                    p.Id,
                    p.RoleId,
                    p.ControleId,
                    controle.Libelle,
                    controle.Category,
                    p.IsGranted,
                    TenantId: _tenantContext.TenantId
                ));
            }

            return result;
        }

        public async Task AllowAsync(Guid roleId, Guid controleId)
        {
            Role role = await _roleRepository.GetByIdAsync(roleId) ?? throw new RoleNotFoundException(roleId);

            Controle controle = await _controleRepository.GetByIdAsync(controleId) ?? throw new ControleNotFoundException(controleId);

            Privilege? privilege = await _privilegeRepository.GetByRoleIdAndControleIdAsync(roleId, controleId);

            if (privilege == null)
            {
                privilege = new Privilege(roleId, controleId, true, _tenantContext.TenantId);
                await _privilegeRepository.AddAsync(privilege);
            }
            else
            {
                if (privilege.IsGranted) return;

                privilege.SetGranted(true);
                await _privilegeRepository.UpdateAsync(privilege);
            }
        }

        public async Task DenyAsync(Guid roleId, Guid controleId)
        {
            Role role = await _roleRepository.GetByIdAsync(roleId) ?? throw new RoleNotFoundException(roleId);

            Controle controle = await _controleRepository.GetByIdAsync(controleId) ?? throw new ControleNotFoundException(controleId);


            Privilege? privilege = await _privilegeRepository.GetByRoleIdAndControleIdAsync(roleId, controleId);

            if (privilege == null)
            {
                privilege = new Privilege(roleId, controleId, false, _tenantContext.TenantId);
                await _privilegeRepository.AddAsync(privilege);
            }
            else
            {
                if (!privilege.IsGranted) return;

                privilege.SetGranted(false);
                await _privilegeRepository.UpdateAsync(privilege);
            }
        }
    }
}