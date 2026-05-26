// Application/Services/LocalCache/FournisseurCacheService.cs
using ERP.StockService.Application.DTOs;
using ERP.StockService.Application.Interfaces;
using ERP.StockService.Domain.LocalCache.Fournisseur;
using Microsoft.EntityFrameworkCore;

namespace ERP.StockService.Application.Services.LocalCache.Fournisseur;

public class FournisseurCacheService : IFournisseurCacheService
{
    private readonly IFournisseurCacheRepository _repository;
    private readonly ILogger<FournisseurCacheService> _logger;

    public FournisseurCacheService(
        IFournisseurCacheRepository repository,
        ILogger<FournisseurCacheService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    // =========================
    // READ OPERATIONS
    // =========================

    public async Task<FournisseurResponseDto?> GetByIdAsync(Guid id)
    {
        try
        {
            FournisseurCache? fournisseur = await _repository.GetByIdAsync(id);
            return fournisseur != null ? MapToDto(fournisseur) : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting fournisseur by ID {FournisseurId}", id);
            throw;
        }
    }

    public async Task<FournisseurResponseDto?> GetByNameAsync(string name)
    {
        try
        {
            FournisseurCache? fournisseur = await _repository.GetByNameAsync(name);
            return fournisseur != null ? MapToDto(fournisseur) : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting fournisseur by name {FournisseurName}", name);
            throw;
        }
    }

    public async Task<FournisseurResponseDto?> GetByTaxNumberAsync(string taxNumber)
    {
        try
        {
            FournisseurCache? fournisseur = await _repository.GetByTaxNumberAsync(taxNumber);
            return fournisseur != null ? MapToDto(fournisseur) : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting fournisseur by tax number {TaxNumber}", taxNumber);
            throw;
        }
    }

    public async Task<PagedResultDto<FournisseurResponseDto>> GetPagedAsync(
        int pageNumber, int pageSize, string? search = null)
    {
        if (pageNumber < 1)
            throw new ArgumentOutOfRangeException(nameof(pageNumber));
        if (pageSize < 1)
            throw new ArgumentOutOfRangeException(nameof(pageSize));

        (List<FournisseurCache>? items, int totalCount) = await _repository
            .GetPagedAsync(pageNumber, pageSize, search);

        List<FournisseurResponseDto> dtos = items.Select(MapToDto).ToList();

        return new PagedResultDto<FournisseurResponseDto>(
            dtos,
            totalCount,
            pageNumber,
            pageSize
        );
    }

    public async Task<List<FournisseurResponseDto>> GetBlockedAsync()
    {
        try
        {
            List<FournisseurCache> fournisseurs = await _repository.GetBlockedAsync();
            return fournisseurs.Select(MapToDto).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting blocked fournisseurs");
            throw;
        }
    }

    public async Task<bool> ExistsAsync(Guid id)
    {
        try
        {
            return await _repository.ExistsAsync(id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking existence for fournisseur {FournisseurId}", id);
            throw;
        }
    }

    // =========================
    // SYNC OPERATIONS
    // =========================

    public async Task SyncCreatedAsync(FournisseurResponseDto dto)
    {
        if (dto == null) throw new ArgumentNullException(nameof(dto));
        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            _logger.LogWarning("Fournisseur event has null or empty Name. Id: {FournisseurId}", dto.Id);
            return;
        }

        try
        {
            // If the exact same ID already exists, just update it
            FournisseurCache? existing = await _repository.GetByIdAsync(dto.Id);
            if (existing != null)
            {
                _logger.LogInformation("Fournisseur {FournisseurId} already exists. Updating.", dto.Id);
                existing.ApplyUpdate(dto);
                await _repository.UpdateAsync(existing);
                await _repository.SaveChangesAsync();
                return;
            }

            // Name collision — update the existing record in place (don't delegate via DTO)
            FournisseurCache? existingByName = await _repository.GetByNameAsync(dto.Name);
            if (existingByName != null)
            {
                _logger.LogWarning(
                    "Name '{Name}' already exists (ID {ExistingId}). Applying update to existing record.",
                    dto.Name, existingByName.Id);
                existingByName.ApplyUpdate(dto);
                await _repository.UpdateAsync(existingByName);
                await _repository.SaveChangesAsync();
                return;
            }

            // Email collision — same pattern
            if (!string.IsNullOrWhiteSpace(dto.Email))
            {
                FournisseurCache? existingByEmail = await _repository.GetByEmailAsync(dto.Email);
                if (existingByEmail != null)
                {
                    _logger.LogWarning(
                        "Email '{Email}' already exists (ID {ExistingId}). Applying update to existing record.",
                        dto.Email, existingByEmail.Id);
                    existingByEmail.ApplyUpdate(dto);
                    await _repository.UpdateAsync(existingByEmail);
                    await _repository.SaveChangesAsync();
                    return;
                }
            }

            // Tax number collision — just skip (as before)
            FournisseurCache? existingByTax = await _repository.GetByTaxNumberAsync(dto.TaxNumber);
            if (existingByTax != null)
            {
                _logger.LogWarning(
                    "Tax number '{TaxNumber}' already exists (ID {ExistingId}). Skipping.",
                    dto.TaxNumber, existingByTax.Id);
                return;
            }

            // Fresh insert
            FournisseurCache fournisseur = new FournisseurCache(dto);
            await _repository.AddAsync(fournisseur);
            await _repository.SaveChangesAsync();

            _logger.LogInformation("Fournisseur {Name} ({Id}) added to cache.", dto.Name, dto.Id);
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("duplicate") == true)
        {
            _logger.LogWarning(ex, "Duplicate key on insert for {Name}. Already exists.", dto.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing created fournisseur {Name}", dto.Name);
            throw;
        }
    }

    public async Task SyncUpdatedAsync(FournisseurResponseDto dto)
    {
        if (dto == null)
            throw new ArgumentNullException(nameof(dto));

        try
        {
            FournisseurCache? existing = await _repository.GetByIdAsync(dto.Id);
            if (existing == null)
            {
                _logger.LogError(
                    "Fournisseur {FournisseurId} not found for update. Cache may be out of sync. Dropping event.",
                    dto.Id);
                return;
            }

            existing.ApplyUpdate(dto);
            await _repository.UpdateAsync(existing);
            await _repository.SaveChangesAsync();

            _logger.LogInformation("Fournisseur {FournisseurName} (Id: {FournisseurId}) updated in cache",
                dto.Name, dto.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing updated fournisseur {FournisseurName}", dto.Name);
            throw;
        }
    }

    public async Task SyncDeletedAsync(FournisseurResponseDto dto)
    {
        if (dto == null)
            throw new ArgumentNullException(nameof(dto));

        try
        {
            FournisseurCache? existing = await _repository.GetByIdAsync(dto.Id);
            if (existing == null)
            {
                _logger.LogWarning("Fournisseur {FournisseurId} not found for deletion", dto.Id);
                return;
            }

            existing.MarkDeleted();
            await _repository.UpdateAsync(existing);
            await _repository.SaveChangesAsync();

            _logger.LogInformation("Fournisseur {FournisseurName} (Id: {FournisseurId}) marked as deleted in cache",
                dto.Name, dto.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing deleted fournisseur {FournisseurId}", dto.Id);
            throw;
        }
    }

    public async Task SyncRestoredAsync(FournisseurResponseDto dto)
    {
        if (dto == null)
            throw new ArgumentNullException(nameof(dto));

        try
        {
            FournisseurCache? existing = await _repository.GetByIdAsync(dto.Id);
            if (existing == null)
            {
                _logger.LogWarning("Fournisseur {FournisseurId} not found for restore", dto.Id);
                return;
            }

            existing.MarkRestored();
            await _repository.UpdateAsync(existing);
            await _repository.SaveChangesAsync();

            _logger.LogInformation("Fournisseur {FournisseurName} (Id: {FournisseurId}) restored in cache",
                dto.Name, dto.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing restored fournisseur {FournisseurId}", dto.Id);
            throw;
        }
    }

    public async Task SyncBlockedAsync(Guid id, bool isBlocked)
    {
        try
        {
            FournisseurCache? existing = await _repository.GetByIdAsync(id);
            if (existing == null)
            {
                _logger.LogWarning("Fournisseur {FournisseurId} not found for block/unblock operation", id);
                return;
            }

            if (isBlocked)
                existing.Block();
            else
                existing.Unblock();

            await _repository.UpdateAsync(existing);
            await _repository.SaveChangesAsync();

            _logger.LogInformation("Fournisseur {FournisseurName} (Id: {FournisseurId}) {Action} in cache",
                existing.Name, id, isBlocked ? "blocked" : "unblocked");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing blocked status for fournisseur {FournisseurId}", id);
            throw;
        }
    }

    // =========================
    // PRIVATE HELPERS
    // =========================

    private static FournisseurResponseDto MapToDto(FournisseurCache fournisseur)
    {
        return new FournisseurResponseDto(
            Id: fournisseur.Id,
            Name: fournisseur.Name,
            Address: fournisseur.Address,
            Phone: fournisseur.Phone,
            Email: fournisseur.Email,
            TaxNumber: fournisseur.TaxNumber,
            RIB: fournisseur.RIB,
            IsDeleted: fournisseur.IsDeleted,
            IsBlocked: fournisseur.IsBlocked,
            CreatedAt: fournisseur.CreatedAt,
            UpdatedAt: fournisseur.UpdatedAt,
            TenantId: fournisseur.TenantId
        );
    }
}