using ERP.ArticleService.Application.DTOs;
using ERP.ArticleService.Application.Interfaces;
using ERP.ArticleService.Domain;

namespace ERP.ArticleService.Infrastructure.Persistence.Seeders
{
    public class CategorySeeder
    {
        private readonly ICategoryRepository _categoryRepository;
        private readonly ILogger<CategorySeeder> _logger;

        public CategorySeeder(ICategoryRepository categoryRepository, ILogger<CategorySeeder> logger)
        {
            _categoryRepository = categoryRepository;
            _logger = logger;
        }

        public async Task SeedAsync()
        {
            var existing = await _categoryRepository.GetAllAsync();
            var existingNames = new HashSet<string>(existing.Select(c => c.Name));

            foreach ((string name, int tva) in SeedDataConstants.Categories.All)
            {
                if (existingNames.Contains(name))
                {
                    _logger.LogInformation("→ Category '{Name}' already exists, skipping.", name);
                    continue;
                }

                try
                {
                    Category category = new Category(name, tva);
                    await _categoryRepository.AddAsync(category);
                    await _categoryRepository.SaveChangesAsync();
                    _logger.LogInformation("✓ Seeded category: '{Name}' (TVA: {TVA}%)", name, tva);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "✗ Failed to seed category '{Name}'", name);
                }
            }
        }
    }
}