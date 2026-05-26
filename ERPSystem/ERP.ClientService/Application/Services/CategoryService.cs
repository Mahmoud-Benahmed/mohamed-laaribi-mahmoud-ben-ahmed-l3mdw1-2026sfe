using ERP.ClientService.Application.DTOs;
using ERP.ClientService.Application.Exceptions;
using ERP.ClientService.Application.Interfaces;
using ERP.ClientService.Domain;
using ERP.ClientService.Infrastructure.Messaging;

namespace ERP.ClientService.Application.Services;

public class CategoryService : ICategoryService
{
    private readonly ICategoryRepository _categoryRepository;
    private readonly IEventPublisher _eventPublisher;
    private readonly ITenantContext _tenantContext;

    public CategoryService(ICategoryRepository categoryRepository, IEventPublisher eventPublisher, ITenantContext tenantContext)
    {
        _categoryRepository = categoryRepository;
        _eventPublisher = eventPublisher;
        _tenantContext = tenantContext;
    }

    // =========================
    // CREATE
    // =========================
    public async Task<CategoryResponseDto> CreateAsync(CreateCategoryRequestDto request)
    {
        Category? existing = await _categoryRepository.GetByCodeAsync(request.Code);
        if (existing is not null)
            throw new CategoryAlreadyExistsException(request.Code);

        if (!request.UseBulkPricing && request.DiscountRate != null)
            throw new ArgumentException("Discount not allowed without bulk pricing");

        Category category = Category.Create(
            request.Name, request.Code, request.DelaiRetour, request.DuePaymentPeriod,
            request.UseBulkPricing, request.DiscountRate, request.CreditLimitMultiplier,
            _tenantContext.TenantId);

        await _categoryRepository.AddAsync(category);
        await _categoryRepository.SaveChangesAsync();

        CategoryResponseDto res = category.ToResponseDto();
        await _eventPublisher.PublishAsync(CategoryTopics.Created, res);
        return res;
    }

    // =========================
    // READ
    // =========================
    public async Task<CategoryResponseDto> GetByIdAsync(Guid id)
    {
        Category? category = await _categoryRepository.GetByIdAsync(id);
        if (category is null || category.IsDeleted)
            throw new CategoryNotFoundException(id);
        return MapToDto(category);
    }

    // =========================
    // UPDATE
    // =========================
    public async Task<CategoryResponseDto> UpdateAsync(Guid id, UpdateCategoryRequestDto request)
    {
        Category? category = await _categoryRepository.GetByIdAsync(id);
        if (category is null || category.IsDeleted)
            throw new CategoryNotFoundException(id);

        string normalised = request.Code.Trim().ToUpperInvariant();
        if (category.Code != normalised)
        {
            Category? existing = await _categoryRepository.GetByCodeAsync(request.Code);
            if (existing is not null)
                throw new CategoryAlreadyExistsException(request.Code);
        }

        category.Update(
            request.Name, request.Code, request.DelaiRetour, request.DuePaymentPeriod,
            request.UseBulkPricing, request.DiscountRate, request.CreditLimitMultiplier);

        await _categoryRepository.SaveChangesAsync();

        CategoryResponseDto res = category.ToResponseDto();
        await _eventPublisher.PublishAsync(CategoryTopics.Updated, res);
        return res;
    }

    // =========================
    // DELETE
    // =========================
    public async Task DeleteAsync(Guid id)
    {
        Category category = await _categoryRepository.GetByIdAsync(id) ?? throw new CategoryNotFoundException(id);

        if (category.ClientCategories != null && category.ClientCategories.Any())
            throw new CategoryAssignedToUsersException("This category is assigned to existing users.");

        category.Delete();
        await _categoryRepository.SaveChangesAsync();

        CategoryResponseDto res = category.ToResponseDto();
        await _eventPublisher.PublishAsync(CategoryTopics.Deleted, res);
    }

    // =========================
    // RESTORE
    // =========================
    public async Task RestoreAsync(Guid id)
    {
        Category category = await _categoryRepository.GetByIdDeletedAsync(id)
            ?? throw new CategoryNotFoundException(id);

        if (!category.IsDeleted)
            return;

        category.Restore();
        await _categoryRepository.SaveChangesAsync();

        CategoryResponseDto res = category.ToResponseDto();
        await _eventPublisher.PublishAsync(CategoryTopics.Restored, res);
    }

    // =========================
    // ACTIVATE / DEACTIVATE
    // =========================
    public async Task<CategoryResponseDto> ActivateAsync(Guid id)
    {
        Category? category = await _categoryRepository.GetByIdAsync(id);
        if (category is null || category.IsDeleted)
            throw new CategoryNotFoundException(id);

        category.Activate();
        await _categoryRepository.SaveChangesAsync();

        CategoryResponseDto res = category.ToResponseDto();
        await _eventPublisher.PublishAsync(CategoryTopics.Updated, res);
        return res;
    }

    public async Task<CategoryResponseDto> DeactivateAsync(Guid id)
    {
        Category? category = await _categoryRepository.GetByIdAsync(id);
        if (category is null || category.IsDeleted)
            throw new CategoryNotFoundException(id);

        category.Deactivate();
        await _categoryRepository.SaveChangesAsync();

        CategoryResponseDto res = category.ToResponseDto();
        await _eventPublisher.PublishAsync(CategoryTopics.Updated, res);
        return MapToDto(category);
    }

    // =========================
    // PAGING / FILTERING
    // =========================
    public async Task<List<CategoryResponseDto>> GetAllAsync()
    {
        List<Category> items = await _categoryRepository.GetAllAsync();
        return items.Select(c => MapToDto(c)).ToList();
    }
    public async Task<PagedResultDto<CategoryResponseDto>> GetAllPagedAsync(
        int pageNumber, int pageSize)
    {
        ValidatePaging(pageNumber, pageSize);
        (List<Category>? items, int totalCount) = await _categoryRepository.GetAllPagedAsync(pageNumber, pageSize);
        return new PagedResultDto<CategoryResponseDto>(items.Select(c => MapToDto(c)).ToList(), totalCount, pageNumber, pageSize);
    }

    public async Task<PagedResultDto<CategoryResponseDto>> GetPagedDeletedAsync(
        int pageNumber, int pageSize)
    {
        ValidatePaging(pageNumber, pageSize);
        (List<Category>? items, int totalCount) = await _categoryRepository
            .GetPagedDeletedAsync(pageNumber, pageSize);
        return new PagedResultDto<CategoryResponseDto>(
            items.Select(c => MapToDto(c)).ToList(), totalCount, pageNumber, pageSize);
    }

    public async Task<PagedResultDto<CategoryResponseDto>> GetPagedByNameAsync(string nameFilter, int pageNumber, int pageSize)
    {
        ValidatePaging(pageNumber, pageSize);
        if (string.IsNullOrWhiteSpace(nameFilter))
            throw new ArgumentException("Name filter cannot be empty.");

        (List<Category>? items, int totalCount) = await _categoryRepository
            .GetPagedByNameAsync(nameFilter, pageNumber, pageSize);
        return new PagedResultDto<CategoryResponseDto>(
            items.Select(c => MapToDto(c)).ToList(), totalCount, pageNumber, pageSize);
    }

    // =========================
    // STATS
    // =========================
    public async Task<CategoryStatsDto> GetStatsAsync() =>
        await _categoryRepository.GetStatsAsync();

    // =========================
    // PRIVATE HELPERS
    // =========================
    private static void ValidatePaging(int pageNumber, int pageSize)
    {
        if (pageNumber < 1)
            throw new ArgumentOutOfRangeException(nameof(pageNumber),
                "Page number must be greater than zero.");
        if (pageSize < 1)
            throw new ArgumentOutOfRangeException(nameof(pageSize),
                "Page size must be greater than zero.");
    }

    public CategoryResponseDto MapToDto(Category category)
    {
        return new CategoryResponseDto(
            Id: category.Id,
            Name: category.Name,
            Code: category.Code,
            DelaiRetour: category.DelaiRetour,
            DuePaymentPeriod: category.DuePaymentPeriod,
            DiscountRate: category.DiscountRate,
            CreditLimitMultiplier: category.CreditLimitMultiplier,
            UseBulkPricing: category.UseBulkPricing,
            IsActive: category.IsActive,
            IsDeleted: category.IsDeleted,
            CreatedAt: category.CreatedAt,
            UpdatedAt: category.UpdatedAt,
            TenantId: category.TenantId
        );
    }
}