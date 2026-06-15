using ERP.AuthService.Application.DTOs.AuthUser;
using ERP.AuthService.Application.DTOs.Role;
using ERP.AuthService.Application.Exceptions;
using ERP.AuthService.Application.Interfaces.Repositories;
using ERP.AuthService.Application.Interfaces.Services;
using ERP.AuthService.Domain;
using ERP.AuthService.Domain.Logger;

namespace ERP.AuthService.Application.Services
{
    public class ControleService : IControleService
    {
        private readonly IAuditLogger _auditLogger;
        private readonly IControleRepository _controleRepository;
        private readonly IPrivilegeRepository _privilegeRepository;
        private readonly ITenantContext _tenantContext;


        public ControleService(IAuditLogger auditLogger,
                                IControleRepository controleRepository,
                                IPrivilegeRepository privilegeRepository,
                                ITenantContext tenantContext)
        {
            _tenantContext = tenantContext;
            _privilegeRepository = privilegeRepository;
            _auditLogger = auditLogger;
            _controleRepository = controleRepository;
        }

        /// <summary>
        /// Get all controles.
        /// </summary>
        public async Task<PagedResultDto<ControleResponseDto>> GetAllPagedAsync(int pageNumber, int pageSize)
        {
            ValidatePaging(pageNumber, pageSize);
            (List<Controle>? items, int totalCount) = await _controleRepository.GetAllPagedAsync(pageNumber, pageSize);
            List<ControleResponseDto> mapped = items.Select(MapToDto).ToList();
            return new PagedResultDto<ControleResponseDto>(
                mapped,
                totalCount,
                pageNumber,
                pageSize);
        }

        public async Task<List<ControleResponseDto>> GetAllAsync()
        {
            List<Controle> items = await _controleRepository.GetAllAsync();
            List<ControleResponseDto> mapped = items.Select(MapToDto).ToList();
            return mapped;
        }

        /// <summary>
        /// Get all controles belonging to a given category (case-insensitive).
        /// </summary>
        public async Task<PagedResultDto<ControleResponseDto>> GetByCategoryAsync(string category, int pageNum, int pageSize)
        {
            ValidatePaging(pageNum, pageSize);
            (List<Controle>? items, int totalCount) = await _controleRepository.GetByCategoryAsync(category, pageNum, pageSize);
            return new PagedResultDto<ControleResponseDto>(
                items.Select(MapToDto).ToList(),
                totalCount,
                pageNum,
                pageSize);
        }

        /// <summary>
        /// Get a controle by its ID.
        /// </summary>
        public async Task<ControleResponseDto> GetByIdAsync(Guid id)
        {
            Controle? controle = await _controleRepository.GetByIdAsync(id);

            if (controle is null)
                throw new ControleNotFoundException(id);
            return MapToDto(controle);
        }

        /// <summary>
        /// Create a new controle.
        /// </summary>
        public async Task<ControleResponseDto> CreateControleAsync(ControleRequestDto request, Guid requesterId)
        {
            if (await _controleRepository.DuplicateExists(request.Libelle))
                throw new DuplicateKeyException($"User.Libelle: {request.Libelle}");

            Controle controle = new Controle(request.Category, request.Libelle, request.Description, tenantId: _tenantContext.TenantId);
            await _controleRepository.AddAsync(controle);
            await _auditLogger.LogAsync(
                        AuditAction.ControleCreated,
                        success: true,
                        performedBy: requesterId,
                        metadata: new() { ["created"] = request.Libelle.ToString(), ["createdBy"] = requesterId.ToString() });

            return MapToDto(controle);

        }

        /// <summary>
        /// Update an existing controle.
        /// </summary>
        public async Task<ControleResponseDto> UpdateControleAsync(Guid id, ControleRequestDto request, Guid requesterId)
        {

            Controle? existing = await _controleRepository.GetByIdAsync(id);
            if (existing is null)
                throw new KeyNotFoundException($"Controle with ID '{id}' was not found.");

            if (await _controleRepository.DuplicateExists(request.Libelle, id))
                throw new DuplicateKeyException($"User.Libelle: {request.Libelle}");

            existing.Update(request);

            await _controleRepository.UpdateAsync(existing);
            await _auditLogger.LogAsync(
                        AuditAction.ControleUpdated,
                        success: true,
                        performedBy: requesterId,
                        metadata: new()
                        {
                            ["controleId"] = id.ToString(),
                            ["newLibelle"] = request.Libelle,
                            ["newCategory"] = request.Category,
                            ["performedBy"] = requesterId.ToString()
                        });

            return MapToDto(existing);
        }

        /// <summary>
        /// Delete a controle by its ID.
        /// </summary>
        public async Task DeleteByIdAsync(Guid id, Guid requesterId)
        {
            Controle existing = await _controleRepository.GetByIdAsync(id) ?? throw new KeyNotFoundException($"Controle with ID '{id}' was not found.");


            await _privilegeRepository.DeleteByControleIdAsync(id);
            await _controleRepository.DeleteAsync(id);

            await _auditLogger.LogAsync(
                        AuditAction.ControleDeleted,
                        success: true,
                        performedBy: requesterId,
                        metadata: new() { ["deleted"] = existing.ToString(), ["performedBy"] = requesterId.ToString() });
        }

        /// <summary>
        /// Delete all controles.
        /// </summary>
        public async Task DeleteAllAsync()
        {
            await _controleRepository.DeleteAllAsync();
        }

        // ── Mapping ───────────────────────────────────────────────────────────

        private static ControleResponseDto MapToDto(Controle controle) =>
            new(
                controle.Id,
                controle.Category,
                controle.Libelle,
                controle.Description
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