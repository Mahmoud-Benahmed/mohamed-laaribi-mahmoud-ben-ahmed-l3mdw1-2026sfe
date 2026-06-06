using ERP.InvoiceService.Application.DTOs;
using ERP.InvoiceService.Application.Interfaces;
using ERP.InvoiceService.Domain.LocalCache.Client;
using InvoiceService.Application.DTOs;
using Microsoft.EntityFrameworkCore;

namespace ERP.InvoiceService.Application.Services.LocalCache;

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

        // Check if client already exists
        Domain.LocalCache.Client.ClientCache? existing = await _clientCacheRepository.GetByIdAsync(dto.Id);
        if (existing != null)
        {
            _logger.LogInformation("Client {ClientName} (Id: {ClientId}) already exists in cache. Cancelling.",
                dto.Name, dto.Id);
            return;
        }

        // Create new client with all parameters
        Domain.LocalCache.Client.ClientCache clientCache = Domain.LocalCache.Client.ClientCache.Create(dto);

        await _clientCacheRepository.AddAsync(clientCache);

        // Handle categories if any
        if (dto.Categories != null && dto.Categories.Any())
        {
            await AssignCategoriesToClientAsync(clientCache.Id, dto.Categories);
        }

        await _clientCacheRepository.SaveChangesAsync();

        _logger.LogInformation("Client {ClientName} (Id: {ClientId}) added to cache with {CategoryCount} categories",
                dto.Name, dto.Id, dto.Categories?.Count() ?? 0);
    }

    public async Task SyncUpdatedAsync(ClientResponseDto dto)
    {
        Domain.LocalCache.Client.ClientCache? existing = await _clientCacheRepository.GetByIdAsync(dto.Id) ?? await _clientCacheRepository.GetByEmailAsync(dto.Email);
        if (existing == null)
        {
            _logger.LogWarning("Client {ClientId} not found for update. Cancelling...", dto.Id);
            return;
        }

        // Update client basic info
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
        await _clientCacheRepository.SaveChangesAsync();

        // Update categories if needed
        if (dto.Categories != null)
        {
            await UpdateClientCategoriesAsync(existing.Id, dto.Categories);
        }


        _logger.LogInformation("Client {ClientName} (Id: {ClientId}) updated in cache", existing.Name, existing.Id);
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

        Domain.LocalCache.Client.ClientCache? existing = await _clientCacheRepository.GetByIdDeletedAsync(dto.Id);
        if (existing == null)
        {
            _logger.LogWarning("Client {ClientId} not found for restore", dto.Id);
            return;
        }

        existing.Restore();
        await _clientCacheRepository.UpdateAsync(existing);

        if (dto.Categories?.Any() == true)
            await AssignCategoriesToClientAsync(existing.Id, dto.Categories);

        await _clientCacheRepository.SaveChangesAsync(); // ← save restore + assignments together

        _logger.LogInformation("Client {ClientName} (Id: {ClientId}) restored in cache", existing.Name, existing.Id);
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
                await _clientCategoryRepository.SaveChangesAsync();
            }

            bool alreadyAssigned = await _clientCategoryRepository.ExistsForClientAsync(clientId, categoryDto.Id);
            if (!alreadyAssigned)
            {
                await _clientCategoryRepository.AssignCategoryToClientAsync(clientId, category.Id);
                await _clientCategoryRepository.SaveChangesAsync(); // ← FK is satisfied, row persists
            }
            else
            {
                _logger.LogDebug(
                    "Category {CategoryId} already assigned to client {ClientId}, skipping.",
                    categoryDto.Id, clientId);
            }
        }
    }


    private async Task UpdateClientCategoriesAsync(
        Guid clientId, IEnumerable<ClientCategoryResponseDto> newCategories)
    {
        List<ClientCategoryCache> existingAssignments =
            await _clientCategoryRepository.GetClientAssignmentsAsync(clientId);

        HashSet<Guid> existingCategoryIds = existingAssignments
            .Select(a => a.CategoryId).ToHashSet();
        HashSet<Guid> newCategoryIds = newCategories
            .Select(c => c.Id).ToHashSet();

        // ── 1. Stage all removals ─────────────────────────────────────────────
        foreach (Guid categoryId in existingCategoryIds.Except(newCategoryIds))
            await _clientCategoryRepository.UnassignCategoryFromClientAsync(clientId, categoryId);

        // ── 2. Stage all category upserts ────────────────────────────────────
        foreach (Guid categoryId in newCategoryIds.Except(existingCategoryIds))
        {
            ClientCategoryResponseDto categoryDto = newCategories.First(c => c.Id == categoryId);
            CategoryCache? category = await _clientCategoryRepository.GetByIdAsync(categoryId);

            if (category is null)
            {
                category = CategoryCache.Create(categoryDto);
                await _clientCategoryRepository.AddCategoryAsync(category);
            }
            else
            {
                category.Update(
                    name: categoryDto.Name,
                    code: categoryDto.Code,
                    delaiRetour: categoryDto.DelaiRetour,
                    duePaymentPeriod: categoryDto.DuePaymentPeriod,
                    discountRate: categoryDto.DiscountRate,
                    creditLimitMultiplier: categoryDto.CreditLimitMultiplier,
                    useBulkPricing: categoryDto.UseBulkPricing,
                    isActive: categoryDto.IsActive,
                    isDeleted: categoryDto.IsDeleted,
                    createdAt: categoryDto.CreatedAt,
                    updatedAt: categoryDto.UpdatedAt
                );
                await _clientCategoryRepository.UpdateCategoryAsync(category);
            }
        }

        // ── 3. First save — persists category rows, satisfies FK ─────────────
        await _clientCategoryRepository.SaveChangesAsync();

        // ── 4. Stage all new assignments (FK now satisfied) ───────────────────
        foreach (Guid categoryId in newCategoryIds.Except(existingCategoryIds))
            await _clientCategoryRepository.AssignCategoryToClientAsync(clientId, categoryId);

        // ── 5. Second save — persists removals + assignments ──────────────────
        await _clientCategoryRepository.SaveChangesAsync();
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
            DuePaymentPeriod: client.DuePaymentPeriod,
            IsBlocked: client.IsBlocked,
            IsDeleted: client.IsDeleted,
            CreatedAt: client.CreatedAt,
            UpdatedAt: client.UpdatedAt,
            Categories: categoryDtos,
            TenantId: client.TenantId
        );
    }
}