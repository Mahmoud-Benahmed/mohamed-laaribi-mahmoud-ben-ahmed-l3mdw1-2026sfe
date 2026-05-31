using ERP.StockService.Application.DTOs;
using ERP.StockService.Application.Interfaces;
using ERP.StockService.Domain.LocalCache.Client;
using Microsoft.EntityFrameworkCore;

namespace ERP.StockService.Application.Services.LocalCache;

public class ClientCacheService : IClientCacheService
{
    private readonly IClientCacheRepository _clientCacheRepository;
    private readonly IClientCategoryCacheRepository _clientCategoryRepository;
    private readonly ILogger<ClientCacheService> _logger;

    public ClientCacheService(
        IClientCacheRepository clientCacheRepository,
        IClientCategoryCacheRepository clientCategoryRepository,
        ILogger<ClientCacheService> logger)
    {
        _clientCacheRepository = clientCacheRepository;
        _clientCategoryRepository = clientCategoryRepository;
        _logger = logger;
    }

    public async Task<ClientResponseDto?> GetByIdAsync(Guid id)
    {
        Domain.LocalCache.Client.ClientCache? client = await _clientCacheRepository.GetByIdAsync(id);
        return client != null ? MapToDto(client) : null;
    }

    public async Task<PagedResultDto<ClientResponseDto>> GetPagedAsync(
        int pageNumber, int pageSize, string? search = null)
    {
        if (pageNumber < 1)
            throw new ArgumentOutOfRangeException(nameof(pageNumber));
        if (pageSize < 1)
            throw new ArgumentOutOfRangeException(nameof(pageSize));

        (List<Domain.LocalCache.Client.ClientCache>? items, int totalCount) = await _clientCacheRepository
            .GetPagedAsync(pageNumber, pageSize, search); // ← pass search down

        List<ClientResponseDto> dtos = items.Select(MapToDto).ToList();

        return new PagedResultDto<ClientResponseDto>(
            dtos,
            totalCount,
            pageNumber,
            pageSize
        );
    }

    public async Task<bool> ExistsAsync(Guid id)
    {
        return await _clientCacheRepository.ExistsAsync(id);
    }
    public async Task SyncCreatedAsync(ClientResponseDto dto)
    {
        try
        {
            Domain.LocalCache.Client.ClientCache? existing = await _clientCacheRepository.GetByIdAsync(dto.Id);
            if (existing != null)
            {
                _logger.LogInformation("Client {ClientName} (Id: {ClientId}) already exists in cache. Updating instead.",
                    dto.Name, dto.Id);
                await SyncUpdatedAsync(dto);
                return;
            }

            Domain.LocalCache.Client.ClientCache? existingByName = await _clientCacheRepository.GetByNameAsync(dto.Name);
            Domain.LocalCache.Client.ClientCache? existingByEmail = await _clientCacheRepository.GetByEmailAsync(dto.Email);

            if (existingByName != null)
            {
                _logger.LogWarning(
                    "Client name '{ClientName}' already exists with different ID. Existing: {ExistingId}, New: {NewId}. Updating existing client.",
                    dto.Name, existingByName.Id, dto.Id);
                existing = existingByName;
            }
            else if (existingByEmail != null)
            {
                _logger.LogWarning(
                    "Client email '{ClientEmail}' already exists with different ID. Existing: {ExistingId}, New: {NewId}. Updating existing client.",
                    dto.Email, existingByEmail.Id, dto.Id);
                existing = existingByEmail;
            }

            if (existing != null)
            {
                await SyncUpdatedAsync(dto);
                return;
            }

            // Create new client
            Domain.LocalCache.Client.ClientCache clientCache = Domain.LocalCache.Client.ClientCache.Create(dto);
            await _clientCacheRepository.AddAsync(clientCache);

            if (dto.Categories != null && dto.Categories.Any())
            {
                await AssignCategoriesToClientAsync(clientCache.Id, dto.Categories);
            }

            await _clientCacheRepository.SaveChangesAsync();

            _logger.LogInformation("Client {ClientName} (Id: {ClientId}) added to cache with {CategoryCount} categories",
                dto.Name, dto.Id, dto.Categories?.Count() ?? 0);
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("duplicate") == true)
        {
            _logger.LogWarning(ex, "Duplicate client detected for {ClientName}. This is expected if client already exists.",
                dto.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing created client {ClientName}", dto.Name);
            throw;
        }
    }

    public async Task SyncUpdatedAsync(ClientResponseDto dto)
    {
        if (dto == null)
            throw new ArgumentNullException(nameof(dto));

        try
        {
            Domain.LocalCache.Client.ClientCache? existing =
                await _clientCacheRepository.GetByIdAsync(dto.Id)
                ?? throw new KeyNotFoundException($"ClientCache not found with Id `{dto.Id}`");

            existing.Update(
                name: dto.Name,
                email: dto.Email,
                address: dto.Address,
                phone: dto.Phone,
                taxNumber: dto.TaxNumber,
                creditLimit: dto.CreditLimit,
                delaiRetour: dto.DelaiRetour,
                duePaymentPeriod: dto.DuePaymentPeriod,
                isBlocked: dto.IsBlocked,
                isDeleted: dto.IsDeleted,
                createdAt: dto.CreatedAt,
                updatedAt: dto.UpdatedAt
            );

            await _clientCacheRepository.UpdateAsync(existing);

            if (dto.Categories != null)
            {
                await UpdateClientCategoriesAsync(existing.Id, dto.Categories);
            }

            await _clientCacheRepository.SaveChangesAsync();

            _logger.LogInformation("Client {ClientName} (Id: {ClientId}) updated in cache", existing.Name, existing.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing updated client {ClientName}", dto.Name);
            throw;
        }
    }

    public async Task SyncDeletedAsync(ClientResponseDto dto)
    {
        if (dto == null)
            throw new ArgumentNullException(nameof(dto));

        try
        {
            Domain.LocalCache.Client.ClientCache? existing = await _clientCacheRepository.GetByIdAsync(dto.Id) ?? await _clientCacheRepository.GetByEmailAsync(dto.Email);
            if (existing == null)
            {
                _logger.LogWarning("Client {ClientId} not found for deletion", dto.Id);
                return;
            }

            existing.Delete();
            await _clientCacheRepository.UpdateAsync(existing);

            // Optionally remove category assignments
            await _clientCategoryRepository.DeleteAllCategoriesForClientAsync(existing.Id);

            await _clientCacheRepository.SaveChangesAsync();

            _logger.LogInformation("Client {ClientName} (Id: {ClientId}) marked as deleted in cache", existing.Name, existing.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing deleted client {ClientId}", dto.Id);
            throw;
        }
    }

    public async Task SyncRestoredAsync(ClientResponseDto dto)
    {
        if (dto == null)
            throw new ArgumentNullException(nameof(dto));

        try
        {
            Domain.LocalCache.Client.ClientCache? existing = await _clientCacheRepository.GetByIdDeletedAsync(dto.Id);
            if (existing == null)
            {
                _logger.LogWarning("Client {ClientId} not found for restore", dto.Id);
                return;
            }

            existing.Restore();
            await _clientCacheRepository.UpdateAsync(existing);

            // Restore category assignments if needed
            if (dto.Categories != null && dto.Categories.Any())
            {
                await AssignCategoriesToClientAsync(existing.Id, dto.Categories);
            }

            await _clientCacheRepository.SaveChangesAsync();

            _logger.LogInformation("Client {ClientName} (Id: {ClientId}) restored in cache", existing.Name, existing.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing restored client {ClientId}", dto.Id);
            throw;
        }
    }

    // =========================
    // PRIVATE HELPER METHODS
    // =========================

    private async Task AssignCategoriesToClientAsync(Guid clientId, IEnumerable<ClientCategoryResponseDto> categories)
    {
        foreach (ClientCategoryResponseDto categoryDto in categories)
        {
            // Check if category exists in master data
            CategoryCache? category = await _clientCategoryRepository.GetByIdAsync(categoryDto.Id);

            if (category == null)
            {
                // Create new category if it doesn't exist
                category = CategoryCache.Create(categoryDto);
                await _clientCategoryRepository.AddCategoryAsync(category);
            }

            // Assign category to client
            await _clientCategoryRepository.AssignCategoryToClientAsync(clientId, category.Id);
        }
    }

    private async Task UpdateClientCategoriesAsync(Guid clientId, IEnumerable<ClientCategoryResponseDto> newCategories)
    {
        // Get existing assignments
        List<ClientCategoryCache> existingAssignments = await _clientCategoryRepository.GetClientAssignmentsAsync(clientId);
        HashSet<Guid> existingCategoryIds = existingAssignments.Select(a => a.CategoryId).ToHashSet();
        HashSet<Guid> newCategoryIds = newCategories.Select(c => c.Id).ToHashSet();

        // Remove categories that are no longer assigned
        IEnumerable<Guid> categoriesToRemove = existingCategoryIds.Except(newCategoryIds);
        foreach (Guid categoryId in categoriesToRemove)
        {
            await _clientCategoryRepository.UnassignCategoryFromClientAsync(clientId, categoryId);
        }

        // Add new categories
        IEnumerable<Guid> categoriesToAdd = newCategoryIds.Except(existingCategoryIds);
        foreach (Guid categoryId in categoriesToAdd)
        {
            ClientCategoryResponseDto categoryDto = newCategories.First(c => c.Id == categoryId);

            // Ensure category exists in master data
            CategoryCache? category = await _clientCategoryRepository.GetByIdAsync(categoryId);
            if (category == null && categoryDto != null)
            {
                category = CategoryCache.Create(categoryDto);
                await _clientCategoryRepository.AddCategoryAsync(category);
            }

            if (category != null)
            {
                await _clientCategoryRepository.AssignCategoryToClientAsync(clientId, category.Id);
            }
        }
    }

    private ClientResponseDto MapToDto(Domain.LocalCache.Client.ClientCache client)
    {
        List<ClientCategoryResponseDto> categoryDtos = client.ClientCategories
            .Select(assignment => new ClientCategoryResponseDto(
                Id: assignment.Category.Id,
                Name: assignment.Category.Name,
                Code: assignment.Category.Code,
                DelaiRetour: assignment.Category.DelaiRetour,
                DuePaymentPeriod: assignment.Category.DuePaymentPeriod,
                DiscountRate: assignment.Category.DiscountRate,
                CreditLimitMultiplier: assignment.Category.CreditLimitMultiplier,
                UseBulkPricing: assignment.Category.UseBulkPricing,
                IsActive: assignment.Category.IsActive,
                IsDeleted: assignment.Category.IsDeleted,
                CreatedAt: assignment.Category.CreatedAt,
                UpdatedAt: assignment.Category.UpdatedAt,
                TenantId: assignment.Category.TenantId
            ))
            .ToList();

        return new ClientResponseDto(
            Id: client.Id,
            Name: client.Name,
            Email: client.Email,
            Address: client.Address,
            Phone: client.Phone,
            TaxNumber: client.TaxNumber,
            CreditLimit: client.CreditLimit,
            DelaiRetour: client.DelaiRetour,
            DuePaymentPeriod: client.DuePaymentPeriod ?? 0,
            IsBlocked: client.IsBlocked,
            IsDeleted: client.IsDeleted,
            CreatedAt: client.CreatedAt,
            UpdatedAt: client.UpdatedAt,
            Categories: categoryDtos,
            TenantId: client.TenantId
        );
    }
}