using ERP.FournisseurService.Application.DTOs;
using ERP.FournisseurService.Application.Exceptions;
using ERP.FournisseurService.Application.Interfaces;
using ERP.FournisseurService.Domain;
using ERP.FournisseurService.Infrastructure.Messaging;

namespace ERP.FournisseurService.Application.Services;

public class FournisseurService : IFournisseurService
{
    private readonly IFournisseurRepository _repo;
    private readonly IEventPublisher _eventPublisher;
    private readonly ITenantContext _tenantContext;

    public FournisseurService(IFournisseurRepository repo, IEventPublisher eventPublisher, ITenantContext tenantContext)
    {
        _repo = repo;
        _eventPublisher = eventPublisher;
        _tenantContext = tenantContext;
    }

    // =========================
    // CREATE
    // =========================
    public async Task<FournisseurResponseDto> CreateAsync(CreateFournisseurRequestDto dto)
    {
        Fournisseur f = Fournisseur.Create(
            dto.Name, dto.Address, dto.Phone, dto.RIB, 
            dto.Email,
            dto.TaxNumber, 
            _tenantContext.TenantId);
        await _repo.AddAsync(f);
        await _repo.SaveChangesAsync();
        FournisseurResponseDto res = f.ToResponseDto();
        await _eventPublisher.PublishAsync(FournisseurTopics.Created, res);
        return res;
    }

    // =========================
    // UPDATE
    // =========================
    public async Task<FournisseurResponseDto> UpdateAsync(Guid id, UpdateFournisseurRequestDto dto)
    {
        Fournisseur f = await _repo.GetByIdAsync(id) ?? throw new FournisseurNotFoundException(id);
        f.Update(dto.Name, dto.Address, dto.Phone, dto.RIB, dto.Email, dto.TaxNumber);
        await _repo.SaveChangesAsync();
        FournisseurResponseDto res = f.ToResponseDto();
        await _eventPublisher.PublishAsync(FournisseurTopics.Updated, res);
        return res;
    }

    // =========================
    // DELETE / RESTORE
    // =========================
    public async Task DeleteAsync(Guid id)
    {
        Fournisseur f = await _repo.GetByIdAsync(id) ?? throw new FournisseurNotFoundException(id);
        f.Delete();
        await _repo.SaveChangesAsync();
        FournisseurResponseDto res = f.ToResponseDto();
        await _eventPublisher.PublishAsync(FournisseurTopics.Deleted, res);
    }

    public async Task RestoreAsync(Guid id)
    {
        Fournisseur f = await _repo.GetByIdDeletedAsync(id) ?? throw new FournisseurNotFoundException(id);
        if (!f.IsDeleted) return;
        f.Restore();
        await _repo.SaveChangesAsync();
        FournisseurResponseDto res = f.ToResponseDto();
        await _eventPublisher.PublishAsync(FournisseurTopics.Restored, res);
    }

    // =========================
    // BLOCK / UNBLOCK
    // =========================
    public async Task<FournisseurResponseDto> BlockAsync(Guid id)
    {
        Fournisseur f = await _repo.GetByIdAsync(id) ?? throw new FournisseurNotFoundException(id);
        f.Block();
        await _repo.SaveChangesAsync();
        FournisseurResponseDto res = f.ToResponseDto();
        await _eventPublisher.PublishAsync(FournisseurTopics.Updated, res);
        return res;
    }

    public async Task<FournisseurResponseDto> UnblockAsync(Guid id)
    {
        Fournisseur f = await _repo.GetByIdAsync(id) ?? throw new FournisseurNotFoundException(id);
        f.Unblock();
        await _repo.SaveChangesAsync();
        FournisseurResponseDto res = f.ToResponseDto();
        await _eventPublisher.PublishAsync(FournisseurTopics.Updated, res);
        return res;
    }

    // =========================
    // READ
    // =========================
    public async Task<FournisseurResponseDto> GetByIdAsync(Guid id)
    {
        Fournisseur f = await _repo.GetByIdAsync(id) ?? throw new FournisseurNotFoundException(id);
        return f.ToResponseDto();
    }

    public async Task<PagedResultDto<FournisseurResponseDto>> GetAllAsync(int page, int size)
    {
        ValidatePaging(page, size);
        (List<Fournisseur>? items, int total) = await _repo.GetAllAsync(page, size);
        return new PagedResultDto<FournisseurResponseDto>(
            items.Select(f => f.ToResponseDto()).ToList(), total, page, size);
    }

    public async Task<PagedResultDto<FournisseurResponseDto>> GetPagedDeletedAsync(int page, int size)
    {
        ValidatePaging(page, size);
        (List<Fournisseur>? items, int total) = await _repo.GetPagedDeletedAsync(page, size);
        return new PagedResultDto<FournisseurResponseDto>(
            items.Select(f => f.ToResponseDto()).ToList(), total, page, size);
    }

    public async Task<PagedResultDto<FournisseurResponseDto>> GetPagedByNameAsync(
        string nameFilter, int page, int size)
    {
        ValidatePaging(page, size);
        if (string.IsNullOrWhiteSpace(nameFilter))
            throw new ArgumentException("Name filter cannot be empty.", nameof(nameFilter));

        (List<Fournisseur>? items, int total) = await _repo.GetPagedByNameAsync(nameFilter, page, size);
        return new PagedResultDto<FournisseurResponseDto>(
            items.Select(f => f.ToResponseDto()).ToList(), total, page, size);
    }

    // =========================
    // STATS
    // =========================
    public async Task<FournisseurStatsDto> GetStatsAsync() =>
        await _repo.GetStatsAsync();

    // =========================
    // HELPERS
    // =========================
    private static void ValidatePaging(int page, int size)
    {
        if (page < 1) throw new ArgumentOutOfRangeException(nameof(page),
            "Page number must be greater than zero.");
        if (size < 1) throw new ArgumentOutOfRangeException(nameof(size),
            "Page size must be greater than zero.");
    }
}