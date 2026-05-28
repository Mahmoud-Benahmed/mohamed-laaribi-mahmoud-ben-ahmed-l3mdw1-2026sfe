using ERP.StockService.Application.DTOs;
using ERP.StockService.Application.Interfaces;
using ERP.StockService.Domain.LocalCache.Client;
using Microsoft.EntityFrameworkCore;

namespace ERP.StockService.Application.Services.LocalCache.ClientCache;

public class ClientCategoryCacheService : IClientCategoryCacheService
{
    private readonly IClientCategoryCacheRepository _repository;
    private readonly IClientCacheRepository _clientRepository;
    private readonly ILogger<ClientCategoryCacheService> _logger;

    public ClientCategoryCacheService(
        IClientCategoryCacheRepository repository,
        IClientCacheRepository clientRepository,
        ILogger<ClientCategoryCacheService> logger)
    {
        _repository = repository;
        _clientRepository = clientRepository;
        _logger = logger;
    }

    // =========================
    // READ OPERATIONS
    // =========================

    public async Task<ClientCategoryResponseDto?> GetByIdAsync(Guid id)
    {
        try
        {
            CategoryCache? category = await _repository.GetByIdAsync(id);
            return category != null ? await MapToDto(category) : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting client category by ID {CategoryId}", id);
            throw;
        }
    }

    public async Task<List<ClientCategoryResponseDto>> GetByClientIdAsync(Guid clientId)
    {
        try
        {
            List<CategoryCache> categories = await _repository.GetByClientIdAsync(clientId);
            List<ClientCategoryResponseDto> categoryDtos = new List<ClientCategoryResponseDto>();
            foreach (CategoryCache category in categories)
            {
                categoryDtos.Add(await MapToDto(category));
            }
            return categoryDtos;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting categories for client {ClientId}", clientId);
            throw;
        }
    }

    public async Task<List<ClientCategoryResponseDto>> GetAllAsync()
    {
        try
        {
            List<CategoryCache> categories = await _repository.GetAllAsync();
            List<ClientCategoryResponseDto> categoryDtos = new List<ClientCategoryResponseDto>();
            foreach (CategoryCache category in categories)
            {
                categoryDtos.Add(await MapToDto(category));
            }
            return categoryDtos;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all client categories");
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
            _logger.LogError(ex, "Error checking existence for category {CategoryId}", id);
            throw;
        }
    }

    public async Task<bool> ExistsForClientAsync(Guid clientId, Guid categoryID)
    {
        try
        {
            return await _repository.ExistsForClientAsync(clientId, categoryID);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking category {CategoryId} for client {ClientId}",
                categoryID, clientId);
            throw;
        }
    }

    public async Task<int> GetCountForClientAsync(Guid clientId)
    {
        try
        {
            return await _repository.GetCountForClientAsync(clientId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting category count for client {ClientId}", clientId);
            throw;
        }
    }

    // =========================
    // SYNC OPERATIONS - Master Categories
    // =========================

    public async Task SyncCreatedAsync(ClientCategoryResponseDto dto)
    {
        if (dto == null)
            throw new ArgumentNullException(nameof(dto));

        try
        {
            // Check if category already exists by ID
            CategoryCache? existing = await _repository.GetByIdAsync(dto.Id);
            if (existing != null)
            {
                _logger.LogInformation("Category {CategoryName} already exists, updating instead", existing.Name);
                await SyncUpdatedAsync(dto);
                return;
            }

            // Check if code exists (different ID)
            CategoryCache? existingByCode = await _repository.GetByCodeAsync(dto.Code);
            if (existingByCode != null)
            {
                _logger.LogWarning("Category code '{Code}' already exists (ID {ExistingId}). Updating existing record.",
                    dto.Code, existingByCode.Id);
                existingByCode.Update(
                    name: dto.Name,
                    code: dto.Code,
                    delaiRetour: dto.DelaiRetour,
                    duePaymentPeriod: dto.DuePaymentPeriod,
                    discountRate: dto.DiscountRate,
                    creditLimitMultiplier: dto.CreditLimitMultiplier,
                    useBulkPricing: dto.UseBulkPricing,
                    isActive: dto.IsActive,
                    isDeleted: dto.IsDeleted,
                    createdAt: dto.CreatedAt,
                    updatedAt: dto.UpdatedAt
                );
                await _repository.SaveChangesAsync();
                return;
            }

            // Fresh insert
            CategoryCache category = CategoryCache.Create(dto);
            await _repository.AddCategoryAsync(category);
            await _repository.SaveChangesAsync();

            _logger.LogInformation("Category {CategoryName} (Code: {Code}) added to master data",
                dto.Name, dto.Code);
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("duplicate") == true)
        {
            _logger.LogWarning(ex, "Duplicate category detected for {CategoryName}", dto.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing created category {CategoryName}", dto.Name);
            throw;
        }
    }

    public async Task SyncUpdatedAsync(ClientCategoryResponseDto dto)
    {
        if (dto == null)
            throw new ArgumentNullException(nameof(dto));

        try
        {
            CategoryCache? existing = await _repository.GetByIdAsync(dto.Id) ?? await _repository.GetByCodeAsync(dto.Code);
            if (existing == null)
            {
                _logger.LogWarning("Category {CategoryId} not found for update, creating instead", dto.Id);
                await SyncCreatedAsync(dto);
                return;
            }

            // Check for code conflict if code changed
            if (existing.Code != dto.Code)
            {
                CategoryCache? existingByCode = await _repository.GetByCodeAsync(dto.Code);
                if (existingByCode != null && existingByCode.Id != dto.Id)
                {
                    _logger.LogError(
                        "Cannot update category {CategoryId}: Code '{Code}' already used by category {ExistingId}",
                        dto.Id, dto.Code, existingByCode.Id);
                    throw new InvalidOperationException($"Category code '{dto.Code}' already exists");
                }
            }

            // Update category properties
            existing.Update(
                name: dto.Name,
                code: dto.Code,
                delaiRetour: dto.DelaiRetour,
                duePaymentPeriod: dto.DuePaymentPeriod,
                discountRate: dto.DiscountRate,
                creditLimitMultiplier: dto.CreditLimitMultiplier,
                useBulkPricing: dto.UseBulkPricing,
                isActive: dto.IsActive,
                isDeleted: dto.IsDeleted,
                createdAt: dto.CreatedAt,
                updatedAt: dto.UpdatedAt
            );

            await _repository.SaveChangesAsync();

            _logger.LogInformation("Category {CategoryName} (Code: {Code}) updated", dto.Name, dto.Code);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing updated category {CategoryId}", dto.Id);
            throw;
        }
    }

    public async Task SyncDeletedAsync(ClientCategoryResponseDto dto)
    {
        if (dto == null)
            throw new ArgumentNullException(nameof(dto));

        try
        {
            CategoryCache? existing = await _repository.GetByIdAsync(dto.Id);
            if (existing == null)
            {
                _logger.LogWarning("Category {CategoryId} not found for deletion", dto.Id);
                return;
            }

            await _repository.DeleteCategoryAsync(dto.Id);
            await _repository.SaveChangesAsync();

            _logger.LogInformation("Category {CategoryName} (Code: {Code}) soft deleted",
                existing.Name, existing.Code);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing deleted category {CategoryId}", dto.Id);
            throw;
        }
    }

    public async Task SyncRestoredAsync(ClientCategoryResponseDto dto)
    {
        if (dto == null)
            throw new ArgumentNullException(nameof(dto));

        try
        {
            CategoryCache? existing = await _repository.GetByIdAsync(dto.Id);
            if (existing == null)
            {
                _logger.LogWarning("Category {CategoryId} not found for restore", dto.Id);
                return;
            }

            existing.Restore();
            await _repository.UpdateCategoryAsync(existing);
            await _repository.SaveChangesAsync();

            _logger.LogInformation("Category {CategoryName} (Code: {Code}) restored",
                existing.Name, existing.Code);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing restored category {CategoryId}", dto.Id);
            throw;
        }
    }

    // =========================
    // CLIENT-CATEGORY ASSIGNMENT OPERATIONS
    // =========================

    public async Task AssignCategoryToClientAsync(Guid clientId, Guid categoryId)
    {
        try
        {
            // Check if client exists
            bool clientExists = await _clientRepository.ExistsAsync(clientId);
            if (!clientExists)
            {
                throw new InvalidOperationException($"Client {clientId} not found");
            }

            // Check if category exists
            bool categoryExists = await _repository.ExistsAsync(categoryId);
            if (!categoryExists)
            {
                throw new InvalidOperationException($"Category {categoryId} not found");
            }

            // Check if already assigned
            bool alreadyAssigned = await _repository.ExistsForClientAsync(clientId, categoryId);
            if (alreadyAssigned)
            {
                _logger.LogInformation("Category {CategoryId} already assigned to client {ClientId}",
                    categoryId, clientId);
                return;
            }

            await _repository.AssignCategoryToClientAsync(clientId, categoryId);
            await _repository.SaveChangesAsync();

            _logger.LogInformation("Category {CategoryId} assigned to client {ClientId} by user",
                categoryId, clientId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assigning category {CategoryId} to client {ClientId}",
                categoryId, clientId);
            throw;
        }
    }

    public async Task UnassignCategoryFromClientAsync(Guid clientId, Guid categoryId)
    {
        try
        {
            await _repository.UnassignCategoryFromClientAsync(clientId, categoryId);
            await _repository.SaveChangesAsync();

            _logger.LogInformation("Category {CategoryId} unassigned from client {ClientId}",
                categoryId, clientId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unassigning category {CategoryId} from client {ClientId}",
                categoryId, clientId);
            throw;
        }
    }

    public async Task SyncRangeCreatedAsync(List<ClientCategoryResponseDto> dtos, Guid clientId)
    {
        if (dtos == null)
            throw new ArgumentNullException(nameof(dtos));

        List<ClientCategoryResponseDto> dtoList = dtos.ToList();
        if (!dtoList.Any())
            return;

        try
        {
            // Check if client exists
            bool clientExists = await _clientRepository.ExistsAsync(clientId);
            if (!clientExists)
            {
                _logger.LogWarning("Client {ClientId} not found when adding categories. Skipping.", clientId);
                return;
            }

            foreach (ClientCategoryResponseDto? dto in dtoList)
            {
                // Sync category master data first
                await SyncCreatedAsync(dto);

                // Then assign to client
                await AssignCategoryToClientAsync(clientId, dto.Id);
            }

            await _repository.SaveChangesAsync();
            _logger.LogInformation("Synced {Count} categories for client {ClientId}",
                dtoList.Count, clientId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing range of categories for client {ClientId}", clientId);
            throw;
        }
    }

    public async Task SyncDeletedForClientAsync(Guid clientId)
    {
        try
        {
            await _repository.DeleteAllCategoriesForClientAsync(clientId);
            await _repository.SaveChangesAsync();
            _logger.LogInformation("All categories unassigned for client {ClientId}", clientId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing deleted categories for client {ClientId}", clientId);
            throw;
        }
    }

    // =========================
    // PRIVATE HELPERS
    // =========================

    private async Task<ClientCategoryResponseDto> MapToDto(CategoryCache category)
    {
        int clientCount = await _repository.GetCountForClientAsync(category.Id);

        return new ClientCategoryResponseDto(
            Id: category.Id,
            Name: category.Name,
            Code: category.Code,
            DelaiRetour: category.DelaiRetour,
            DuePaymentPeriod: category.DuePaymentPeriod,
            DiscountRate: category.DiscountRate ?? 0,
            CreditLimitMultiplier: category.CreditLimitMultiplier ?? 0,
            UseBulkPricing: category.UseBulkPricing,
            IsActive: category.IsActive,
            IsDeleted: category.IsDeleted,
            CreatedAt: category.CreatedAt,
            UpdatedAt: category.UpdatedAt,
            TenantId: category.TenantId
            );
    }
}