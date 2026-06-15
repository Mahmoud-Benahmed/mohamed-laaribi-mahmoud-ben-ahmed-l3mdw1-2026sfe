using ERP.AuthService.Application.DTOs.AuthUser;
using ERP.AuthService.Application.DTOs.Role;
using ERP.AuthService.Application.Exceptions;
using ERP.AuthService.Application.Interfaces.Repositories;
using ERP.AuthService.Application.Interfaces.Services;
using ERP.AuthService.Domain;
using ERP.AuthService.Domain.Logger;

namespace ERP.AuthService.Application.Services
{
    public class RoleService : IRoleService
    {
        private readonly IAuditLogger _auditLogger;
        private readonly IRoleRepository _roleRepository;
        private readonly ITenantContext _tenantContext;
        private readonly IPrivilegeRepository _privilegeRepository;

        public RoleService(IAuditLogger auditLogger, 
            IRoleRepository roleRepository, 
            ITenantContext tenantContext,
            IPrivilegeRepository privilegeRepo)
        {
            _privilegeRepository = privilegeRepo;
            _auditLogger = auditLogger;
            _roleRepository = roleRepository;
            _tenantContext = tenantContext;
        }

        public async Task<PagedResultDto<RoleResponseDto>> GetAllPagedAsync(int pageNumber, int pageSize)
        {
            ValidatePaging(pageNumber, pageSize);
            (List<Role>? items, int totalCount) = await _roleRepository.GetAllPagedAsync(pageNumber, pageSize);
            List<RoleResponseDto> mapped = items.Select(MapToDto).ToList();
            return new PagedResultDto<RoleResponseDto>(
                mapped,
                totalCount,
                pageNumber,
                pageSize);

        }

        public async Task<List<RoleResponseDto>> GetAllAsync()
        {
            List<Role> items = await _roleRepository.GetAllAsync();
            return items.Select(MapToDto).ToList();
        }

        public async Task<RoleResponseDto> GetByIdAsync(Guid id)
        {
            Role role = await _roleRepository.GetByIdAsync(id)
                       ?? throw new RoleNotFoundException(id);

            return MapToDto(role);
        }

        public async Task<RoleResponseDto> CreateRole(RoleCreateDto dto, Guid performedById)
        {
            if (await _roleRepository.DuplicateExists(dto.Libelle))
                throw new DuplicateKeyException($"Role.Libelle: {dto.Libelle}");

            Role role = new Role(dto.Libelle, _tenantContext.TenantId);

            await _roleRepository.AddAsync(role);

            await _auditLogger.LogAsync(
                    AuditAction.RoleCreated,
                    success: true,
                    performedBy: performedById,
                    metadata: new() { ["created"] = dto.Libelle.ToString(), ["createdBy"] = performedById.ToString() });

            return MapToDto(role);
        }
        public async Task<RoleResponseDto> UpdateAsync(Guid id, RoleUpdateDto dto, Guid performedById)
        {
            Role role = await _roleRepository.GetByIdAsync(id) ?? throw new RoleNotFoundException(id);
            string before = role.Libelle.ToString();

            if (await _roleRepository.DuplicateExists(dto.Libelle, id))
                throw new DuplicateKeyException($"Role.Libelle: {dto.Libelle}");

            role.UpdateRole(dto.Libelle);
            await _roleRepository.UpdateAsync(role);

            await _auditLogger.LogAsync(
                    AuditAction.RoleUpdated,
                    success: true,
                    performedBy: performedById,
                    metadata: new()
                    {
                        ["before"] = before,
                        ["after"] = dto.Libelle.ToString(),
                        ["performedBy"] = performedById.ToString()
                    });

            return MapToDto(role);
        }

        public async Task DeleteAsync(Guid id, Guid performedById)
        {
            Role role = await _roleRepository.GetByIdAsync(id) ?? throw new RoleNotFoundException(id);
            
            await _privilegeRepository.DeleteByRoleIdAsync(id);
            await _roleRepository.DeleteAsync(id);
         
            await _auditLogger.LogAsync(
                    AuditAction.RoleDeleted,
                    success: true,
                    performedBy: performedById,
                    metadata: new() { ["deleted"] = role.Libelle.ToString(), ["performedBy"] = performedById.ToString() });

        }

        private static RoleResponseDto MapToDto(Role role) =>
            new(
                role.Id,
                role.Libelle,
                TenantId: role.TenantId
            );

        private static void ValidatePaging(int pageNumber, int pageSize)
        {
            if (pageNumber < 1)
                throw new ArgumentOutOfRangeException(nameof(pageNumber),
                    "Page number must be greater than zero.");
            if (pageSize < 1)
                throw new ArgumentOutOfRangeException(nameof(pageSize),
                    "Page size must be greater than zero.");
        }


    }
}