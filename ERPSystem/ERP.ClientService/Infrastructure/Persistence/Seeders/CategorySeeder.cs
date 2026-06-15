using ERP.ClientService.Application.DTOs;
using ERP.ClientService.Application.Interfaces;
using ERP.ClientService.Domain;

namespace ERP.ClientService.Infrastructure.Persistence.Seeders;

public class CategorySeeder
{
    private readonly ICategoryRepository categoryRepository;
    private readonly ILogger<CategorySeeder> _logger;

    public CategorySeeder(ICategoryRepository _categoryRepository, ILogger<CategorySeeder> logger)
    {
        categoryRepository = _categoryRepository;
        _logger = logger;
    }

    public async Task<List<Category>> SeedAsync()
    {
        // Check if categories already exist
        List<Category> existingCategories = await categoryRepository.GetAllAsync();
        if (existingCategories.Any())
        {
            _logger.LogInformation("Categories already seeded — returning existing.");
            return existingCategories;
        }

        List<Category> categories = BuildCategoryRequests();
        List<Category> createdCategories = new List<Category>();

        foreach (Category request in categories)
        {
            try
            {
                await categoryRepository.AddAsync(request);
                createdCategories.Add(request);
                _logger.LogInformation("Seeded category: {Name} (Code: {Code}, Id: {Id})",
                    request.Name, request.Code, request.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to seed category: {Name}", request.Name);
            }
        }

        // Create and deactivate Legacy category
        try
        {
            Category legacyCategory = Category.Create(
                "Legacy",
                "LGC",
                10,
                30,
                false,
                null,
                null);
            legacyCategory.Deactivate();
            await categoryRepository.AddAsync(legacyCategory);
            createdCategories.Add(legacyCategory);
            _logger.LogInformation("Seeded and deactivated category: Legacy (Id: {Id})", legacyCategory.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to seed Legacy category");
        }


        _logger.LogInformation("Category seeding completed. Created {Count} categories.", createdCategories.Count);
        return createdCategories;
    }

    private List<Category> BuildCategoryRequests() =>
    [
        Category.Create("Standard", "STD", 15, 30, false, null, null),
        Category.Create("VIP", "VIP", 60, 60, true, 0.10m, 1.5m),
        Category.Create("Wholesale", "WHL", 30, 45, true, 0.15m, 2.0m),
        Category.Create("Public Sector", "PUB", 45, 60, false, null, 1.2m),
        Category.Create("Reseller", "RSL", 30, 45, true, 0.20m, 1.8m),
        Category.Create("New Client", "NEW", 7, 15, false, null, null),
    ];
}