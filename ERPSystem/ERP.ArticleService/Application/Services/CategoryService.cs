using ERP.ArticleService.Application.DTOs;
using ERP.ArticleService.Application.Exceptions;
using ERP.ArticleService.Application.Interfaces;
using ERP.ArticleService.Domain;
using ERP.ArticleService.Infrastructure.Messaging;
using ERP.ArticleService.Infrastructure.Persistence;

namespace ERP.ArticleService.Application.Services;
public class CategoryService : ICategoryService
{
    private readonly ITenantContext _tenantContext;
    private readonly ICategoryRepository _categoryRepository;
    private readonly IEventPublisher _eventPublisher; private readonly IArticleRepository _articleRepository;

    public CategoryService(ICategoryRepository categoryRepository, 
                            IEventPublisher eventPublisher, 
                            ITenantContext tenantContext            ,
                            IArticleRepository articleRepository

        )
    {
        _categoryRepository = categoryRepository;
        _eventPublisher = eventPublisher;
        _articleRepository = articleRepository;
        _tenantContext = tenantContext;
    }

    // =========================
    // CREATE
    // =========================
    public async Task<CategoryResponseDto> CreateAsync(CategoryRequestDto dto)
    {
        Category? existing = await _categoryRepository.GetByNameAsync(dto.Name);

        if (existing is not null)
            throw new CategoryAlreadyExistsException(dto.Name);

        Category category = new Category(dto.Name, dto.TVA, _tenantContext.TenantId);
        await _categoryRepository.AddAsync(category);
        await _categoryRepository.SaveChangesAsync();
        CategoryResponseDto res = MapToDto(category);

        await _eventPublisher.PublishAsync(CategoryTopics.Created, res);
        return res;
    }

    // =========================
    // READ
    // =========================
    public async Task<CategoryResponseDto> GetByIdAsync(Guid id)
    {
        Category category = await _categoryRepository.GetByIdAsync(id) ?? throw new CategoryNotFoundException(id);
        return MapToDto(category);
    }

    public async Task<CategoryResponseDto> GetByNameAsync(string name)
    {
        Category category = await _categoryRepository.GetByNameAsync(name) ?? throw new CategoryNotFoundException(name);
        return MapToDto(category);
    }

    public async Task<List<CategoryResponseDto>> GetAllAsync()
    {
        List<Category> result = await _categoryRepository.GetAllAsync();
        return result.Select(MapToDto).ToList();
    }

    public async Task<List<CategoryResponseDto>> GetBelowTVAAsync(decimal tva)
    {
        if (tva <= 0)
            throw new ArgumentException("TVA must be greater than zero.");

        List<Category> result = await _categoryRepository.GetBelowTVAAsync(tva);
        return result.Select(MapToDto).ToList();
    }

    public async Task<List<CategoryResponseDto>> GetHigherThanTVAAsync(decimal tva)
    {
        if (tva <= 0)
            throw new ArgumentException("TVA must be greater than zero.");

        List<Category> result = await _categoryRepository.GetHigherThanTVAAsync(tva);
        return result.Select(MapToDto).ToList();
    }

    public async Task<List<CategoryResponseDto>> GetBetweenTVAAsync(decimal min, decimal max)
    {
        if (min <= 0)
            throw new ArgumentException("Min TVA must be greater than zero.");
        if (max <= 0)
            throw new ArgumentException("Max TVA must be greater than zero.");
        if (min > max)
            throw new ArgumentException("'min' TVA must be less than or equal to 'max' TVA.");

        List<Category> result = await _categoryRepository.GetBetweenTVAAsync(min, max);
        return result.Select(MapToDto).ToList();
    }

    // =========================
    // UPDATE
    // =========================
    public async Task<CategoryResponseDto> UpdateAsync(Guid id, CategoryRequestDto dto)
    {
        Category? existing = await _categoryRepository.GetByNameAsync(dto.Name);
        if (existing is not null && existing.Id != id)
            throw new CategoryAlreadyExistsException(dto.Name);

        Category category = await _categoryRepository.GetByIdAsync(id) ?? throw new CategoryNotFoundException(id);
        category.Update(dto.Name, dto.TVA);

        await _categoryRepository.SaveChangesAsync();

        CategoryResponseDto res = MapToDto(category);

        await _eventPublisher.PublishAsync(CategoryTopics.Updated, res);
        return res;
    }

    // =========================
    // DELETE
    // =========================
    public async Task DeleteAsync(Guid id)
    {
        Category? category = await _categoryRepository.GetByIdAsync(id);
        if (category is null || category.IsDeleted)
            throw new CategoryNotFoundException(id);

        bool hasArticles = await _articleRepository.ExistsForCategoryAsync(id);
        if (hasArticles)
            throw new CategoryAssignedToArticlesException();

        category.Delete();
        await _categoryRepository.SaveChangesAsync();
        CategoryResponseDto dto = MapToDto(category);
        await _eventPublisher.PublishAsync(CategoryTopics.Deleted, dto);
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
        CategoryResponseDto dto = MapToDto(category);
        await _eventPublisher.PublishAsync(CategoryTopics.Restored, dto);
    }

    // =========================
    // PAGING / FILTERING
    // =========================
    public async Task<PagedResultDto<CategoryResponseDto>> GetPagedAsync(
        int pageNumber,
        int pageSize)
    {
        ValidatePaging(pageNumber, pageSize);
        (List<Category>? items, int totalCount) = await _categoryRepository
            .GetPagedAsync(pageNumber, pageSize);

        List<CategoryResponseDto> mappedItems = items.Select(MapToDto).ToList();
        return new PagedResultDto<CategoryResponseDto>(mappedItems, totalCount, pageNumber, pageSize);
    }

    public async Task<PagedResultDto<CategoryResponseDto>> GetPagedDeletedAsync(
        int pageNumber,
        int pageSize)
    {
        ValidatePaging(pageNumber, pageSize);
        (List<Category>? items, int totalCount) = await _categoryRepository.GetDeletedPagedAsync(pageNumber, pageSize);

        List<CategoryResponseDto> mappedItems = items.Select(MapToDto).ToList();
        return new PagedResultDto<CategoryResponseDto>(mappedItems, totalCount, pageNumber, pageSize);
    }

    public async Task<PagedResultDto<CategoryResponseDto>> GetPagedByNameAsync(
        string nameFilter,
        int pageNumber,
        int pageSize)
    {
        ValidatePaging(pageNumber, pageSize);
        if (string.IsNullOrWhiteSpace(nameFilter))
            throw new ArgumentException("Name filter cannot be empty.");

        (List<Category>? items, int totalCount) = await _categoryRepository
            .GetPagedByNameAsync(nameFilter, pageNumber, pageSize);

        List<CategoryResponseDto> mappedItems = items.Select(MapToDto).ToList();
        return new PagedResultDto<CategoryResponseDto>(mappedItems, totalCount, pageNumber, pageSize);
    }

    public async Task<PagedResultDto<CategoryResponseDto>> GetPagedByDateRangeAsync(
        DateTime from,
        DateTime to,
        int pageNumber,
        int pageSize)
    {
        ValidatePaging(pageNumber, pageSize);
        if (from > to)
            throw new ArgumentException("'from' date must be earlier than or equal to 'to' date.");

        (List<Category>? items, int totalCount) = await _categoryRepository
            .GetPagedByDateRangeAsync(from, to, pageNumber, pageSize);

        List<CategoryResponseDto> mappedItems = items.Select(MapToDto).ToList();
        return new PagedResultDto<CategoryResponseDto>(mappedItems, totalCount, pageNumber, pageSize);
    }

    public async Task<CategoryStatsDto> GetStatsAsync()
    {
        return await _categoryRepository.GetStatsAsync();
    }

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

    private static CategoryResponseDto MapToDto(Category category)
    {
        return new CategoryResponseDto(
            Id: category.Id,
            Name: category.Name,
            TVA: category.TVA,
            IsDeleted: category.IsDeleted,
            CreatedAt: category.CreatedAt,
            UpdatedAt: category.UpdatedAt,
            TenantId: category.TenantId
            );
    }
}