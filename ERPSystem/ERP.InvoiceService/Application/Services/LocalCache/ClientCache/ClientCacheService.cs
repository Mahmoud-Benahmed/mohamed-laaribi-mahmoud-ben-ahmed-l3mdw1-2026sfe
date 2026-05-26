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
        if (dto == null)
            throw new ArgumentNullException(nameof(dto));

        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            _logger.LogWarning("Client event has null or empty Name. Id: {ClientId}", dto.Id);
            return;
        }

        try
        {
            // Check if client already exists
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

                await SyncUpdatedAsync(dto);
                return;
            }

            if (existingByEmail != null)
            {
                _logger.LogWarning(
                    "Client email '{ClientEmail}' already exists with different ID. Existing: {ExistingId}, New: {NewId}. Updating existing client.",
                    dto.Email, existingByEmail.Id, dto.Id);

                await SyncUpdatedAsync(dto);
                return;
            }

            _logger.LogInformation(
                "\n\n*> ClientCache created: Id={Id}, Name={Name}, Email={Email}, Address={Address}, Phone={Phone}, TaxNumber={TaxNumber}, CreditLimit={CreditLimit}, DelaiRetour={DelaiRetour}, DuePaymentPeriod={DuePaymentPeriod}, IsBlocked={IsBlocked}, IsDeleted={IsDeleted}, CreatedAt={CreatedAt}, UpdatedAt={UpdatedAt}",
                dto.Id,
                dto.Name,
                dto.Email,
                dto.Address,
                dto.Phone,
                dto.TaxNumber,
                dto.CreditLimit,
                dto.DelaiRetour,
                dto.DuePaymentPeriod,
                dto.IsBlocked,
                dto.IsDeleted,
                dto.CreatedAt,
                dto.UpdatedAt
            );

            foreach (ClientCategoryResponseDto category in dto.Categories)
            {
                _logger.LogInformation(
                    "\n*> Category of {ClientName}: Id={Id}, Name={Name}, Code={Code}, DelaiRetour={DelaiRetour}, DuePaymentPeriod={DuePaymentPeriod}, DiscountRate={DiscountRate}, CreditLimitMultiplier={CreditLimitMultiplier}, UseBulkPricing={UseBulkPricing}, IsActive={IsActive}, IsDeleted={IsDeleted}, CreatedAt={CreatedAt}, UpdatedAt={UpdatedAt}",
                    dto.Name,
                    category.Id,
                    category.Name,
                    category.Code,
                    category.DelaiRetour,
                    category.DuePaymentPeriod,
                    category.DiscountRate,
                    category.CreditLimitMultiplier,
                    category.UseBulkPricing,
                    category.IsActive,
                    category.IsDeleted,
                    category.CreatedAt,
                    category.UpdatedAt
                );
            }

            // Create new client with all parameters
            Domain.LocalCache.Client.ClientCache clientCache = Domain.LocalCache.Client.ClientCache.Create(dto);

            await _clientCacheRepository.AddAsync(clientCache);
            await _clientCacheRepository.SaveChangesAsync();

            // Handle categories if any
            if (dto.Categories != null && dto.Categories.Any())
            {
                await AssignCategoriesToClientAsync(clientCache.Id, dto.Categories);
                await _clientCacheRepository.SaveChangesAsync();
            }


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
            Domain.LocalCache.Client.ClientCache? existing = await _clientCacheRepository.GetByIdAsync(dto.Id) ?? await _clientCacheRepository.GetByEmailAsync(dto.Email);
            if (existing == null)
            {
                _logger.LogWarning("Client {ClientId} not found for update. Creating instead.", dto.Id);
                await SyncCreatedAsync(dto);
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
            Domain.LocalCache.Client.ClientCache? existing = await _clientCacheRepository.GetByIdAsync(dto.Id) ?? await _clientCacheRepository.GetByEmailAsync(dto.Email);
            if (existing == null)
            {
                _logger.LogWarning("Client {ClientId} not found for restore", dto.Id);
                return;
            }

            existing.Restore();
            await _clientCacheRepository.UpdateAsync(existing);
            await _clientCacheRepository.SaveChangesAsync();

            // Restore category assignments if needed
            if (dto.Categories != null && dto.Categories.Any())
            {
                await AssignCategoriesToClientAsync(existing.Id, dto.Categories);
            }


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


    private async Task UpdateClientCategoriesAsync(Guid clientId, IEnumerable<ClientCategoryResponseDto> newCategories)
    {
        List<ClientCategoryCache> existingAssignments = await _clientCategoryRepository.GetClientAssignmentsAsync(clientId);
        HashSet<Guid> existingCategoryIds = existingAssignments.Select(a => a.CategoryId).ToHashSet();
        HashSet<Guid> newCategoryIds = newCategories.Select(c => c.Id).ToHashSet();

        // Remove stale assignments
        foreach (Guid categoryId in existingCategoryIds.Except(newCategoryIds))
        {
            await _clientCategoryRepository.UnassignCategoryFromClientAsync(clientId, categoryId);
        }
        await _clientCategoryRepository.SaveChangesAsync(); // ← flush removals

        // Add new assignments
        foreach (Guid categoryId in newCategoryIds.Except(existingCategoryIds))
        {
            ClientCategoryResponseDto categoryDto = newCategories.First(c => c.Id == categoryId);

            CategoryCache? category = await _clientCategoryRepository.GetByIdAsync(categoryId);
            if (category == null)
            {
                category = CategoryCache.Create(
                    categoryDto
                );
                await _clientCategoryRepository.AddCategoryAsync(category);
                await _clientCategoryRepository.SaveChangesAsync(); // ← category must exist before FK
            }
            else
            {
                // ← Update master data in case name/rules changed on the source service
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
                await _clientCategoryRepository.SaveChangesAsync();
            }

            await _clientCategoryRepository.AssignCategoryToClientAsync(clientId, category.Id);
            await _clientCategoryRepository.SaveChangesAsync(); // ← flush each assignment
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