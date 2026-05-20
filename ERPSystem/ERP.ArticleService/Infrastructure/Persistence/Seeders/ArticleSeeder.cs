using Azure.Core;
using Confluent.Kafka;
using ERP.ArticleService.Application.DTOs;
using ERP.ArticleService.Application.Interfaces;
using ERP.ArticleService.Domain;

namespace ERP.ArticleService.Infrastructure.Persistence.Seeders;

public class ArticleSeeder
{
    private readonly IArticleRepository _articleRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly ILogger<ArticleSeeder> _logger;

    public ArticleSeeder(
        IArticleRepository articleRepository,
        ICategoryRepository categoryRepository,
        ILogger<ArticleSeeder> logger)
    {
        _articleRepository = articleRepository;
        _categoryRepository= categoryRepository;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        var categories = await _categoryRepository.GetAllAsync();
        var categoryDict = categories.ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase);

        // Get existing articles to avoid duplicates (by a unique natural key, e.g., Libelle + CategoryId)
        var existingArticles = await _articleRepository.GetAllAsync(1, 10);
        var existingKeys = new HashSet<(string Libelle, Guid CategoryId)>(
            existingArticles.Items.Select(a => (a.Libelle, a.CategoryId)));

        Random random = new Random();

        foreach (var (libelle, prix, categoryName, unit, tva) in SeedDataConstants.Articles.All)
        {
            if (!categoryDict.TryGetValue(categoryName, out var category))
            {
                _logger.LogWarning("Category '{CategoryName}' not found for article '{Libelle}', skipping.", categoryName, libelle);
                continue;
            }

            // Skip if already seeded (by same libelle and category)
            if (existingKeys.Contains((libelle, category.Id)))
            {
                _logger.LogInformation("Article '{Libelle}' already exists, skipping.", libelle);
                continue;
            }

            try
            {
                string barCode = await GenerateUniqueBarcode(_articleRepository, random);
                string codeRef = await GenerateUniqueCodeRef(_articleRepository, DateTime.Now.Year);

                int validTva = tva > 0 ? tva : 19;
                Article article = new Article(codeRef, libelle, prix, unit, category, barCode, validTva);
                await _articleRepository.AddAsync(article);
                await _articleRepository.SaveChangesAsync();

                _logger.LogInformation("Seeded article: '{Code}' - {Libelle} (TVA: {TVA}%, Unit: {Unit})",
                    article.CodeRef, article.Libelle, article.TVA, article.Unit);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to seed article '{Libelle}' for category '{CategoryName}'.", libelle, categoryName);
            }
        }
    }

    private static string GenerateEAN13(Random random)
    {
        int[] digits = new int[12];
        digits[0] = random.Next(1, 10);
        for (int i = 1; i < 12; i++)
            digits[i] = random.Next(0, 10);
        int sum = 0;
        for (int i = 0; i < 12; i++)
        {
            int multiplier = (i % 2 == 0) ? 1 : 3;
            sum += digits[i] * multiplier;
        }
        int checkDigit = (10 - (sum % 10)) % 10;
        return string.Concat(digits) + checkDigit;
    }

    private static async Task<string> GenerateUniqueBarcode(IArticleRepository repo, Random random)
    {
        string barCode;
        bool exists;
        do
        {
            barCode = GenerateEAN13(random); // pass Random instance
            exists = await repo.GetByCodeAsync(barCode) != null;
        } while (exists);
        return barCode;
    }

    private static async Task<string> GenerateUniqueCodeRef(IArticleRepository repo, int year)
    {
        int counter = 1;
        string codeRef;
        bool exists;
        do
        {
            codeRef = $"ART-{year}-{counter:D5}";
            exists = await repo.GetByCodeAsync(codeRef) != null;
            counter++;
        } while (exists);
        return codeRef;
    }
}