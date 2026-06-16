using ERP.ArticleService.Domain;
using Microsoft.EntityFrameworkCore;
namespace ERP.ArticleService.Infrastructure.Persistence.Seeders
{

    public class ArticleCodeSeeder
    {
        private readonly ArticleDbContext _context;
        private readonly ILogger<ArticleCodeSeeder> _logger;

        public ArticleCodeSeeder(ArticleDbContext context, ILogger<ArticleCodeSeeder> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task SeedAsync(Guid tenantId, string slug)
        {
            // ✅ Check if this tenant already has a sequence row
            bool exists = await _context.ArticleCodes
                .AnyAsync(a => a.TenantId == tenantId);

            if (exists)
            {
                if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development")
                {
                    _logger.LogInformation("ArticleCode row already exists for tenant {TenantId}, skipping.", tenantId);

                }
                return;
            }

            string shortPrefix = slug.Replace("-", "").ToUpperInvariant();
            shortPrefix = shortPrefix.Length > 3 ? shortPrefix[..3] : shortPrefix;
            var articleCode = new ArticleCode(shortPrefix, tenantId, 6);

            await _context.ArticleCodes.AddAsync(articleCode);
            await _context.SaveChangesAsync();
            if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development")
            {
                _logger.LogInformation("ArticleCode config row seeded for tenant {TenantId}: ART, padding 6.", tenantId);
            }
        }
    }
}