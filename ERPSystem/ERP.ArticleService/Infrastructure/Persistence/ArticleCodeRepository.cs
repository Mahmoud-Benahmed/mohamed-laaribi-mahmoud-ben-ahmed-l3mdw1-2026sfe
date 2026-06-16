using ERP.ArticleService.Application.Interfaces;
using ERP.ArticleService.Application.Services;
using ERP.ArticleService.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace ERP.ArticleService.Infrastructure.Persistence
{
    public class ArticleCodeRepository : IArticleCodeRepository
    {
        private readonly ArticleDbContext _context;
        private readonly ITenantContext _tenantContext;

        public ArticleCodeRepository(ArticleDbContext context, ITenantContext tenantContext)
        {
            _context = context;
            _tenantContext = tenantContext;
        }

        public async Task<ArticleCode> AddAsync(ArticleCode code)
        {
            _context.ArticleCodes.Add(code);
            await _context.SaveChangesAsync();
            return code;
        }

        /// <summary>
        /// Generates a new unique article code atomically.
        /// Uses a database transaction with row-level locking to prevent
        /// duplicate codes under concurrent requests.
        /// Example output: "ART-2026-000001", "ART-2026-000042"
        /// </summary>
        public async Task<string> GenerateArticleCodeAsync()
        {
            var tenantId = _tenantContext.TenantId
                ?? throw new InvalidOperationException("Tenant not set.");

            // Build dynamic prefix from tenant slug
            string shortPrefix = _tenantContext.Slug ?? "ART";
            shortPrefix = shortPrefix.Replace("-", "").ToUpperInvariant();
            shortPrefix = shortPrefix.Length > 3 ? shortPrefix[..3] : shortPrefix;

            await using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // Lock and retrieve existing row (SQL Server specific)
                var articleCode = await _context.ArticleCodes
                    .FromSqlRaw(@"
                SELECT TOP 1 *
                FROM ArticleCodes WITH (UPDLOCK, ROWLOCK)
                WHERE TenantId = {0}
                ORDER BY Id", tenantId)
                    .FirstOrDefaultAsync();

                if (articleCode == null)
                {
                    // Create new sequence row
                    articleCode = new ArticleCode(shortPrefix, tenantId);
                    _context.ArticleCodes.Add(articleCode);
                    await _context.SaveChangesAsync();   // persist immediately to avoid duplicate key race
                }
                else
                {
                    // Ensure we have the latest values (reload from DB)
                    await _context.Entry(articleCode).ReloadAsync();
                }

                // Increment and format the code
                articleCode.Increment();
                string generatedCode = articleCode.FormatCode(DateTime.UtcNow.Year);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return generatedCode;
            }
            catch (DbUpdateException)   // Handles unique constraint violation (duplicate TenantId) or concurrency
            {
                await transaction.RollbackAsync();
                // Retry the whole operation once (same as InvoiceNumberGenerator pattern)
                return await GenerateArticleCodeAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
    }
}