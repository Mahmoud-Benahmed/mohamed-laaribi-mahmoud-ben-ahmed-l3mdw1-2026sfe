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
            var tenantId = _tenantContext.TenantId ?? throw new InvalidOperationException("Tenant not set.");


            await using IDbContextTransaction transaction = await _context.Database
                .BeginTransactionAsync();

            try
            {
                ArticleCode articleCode = await _context.ArticleCodes.FromSqlRaw(@"
                                SELECT TOP 1 *
                                FROM ArticleCodes WITH (UPDLOCK, ROWLOCK)
                                WHERE TenantId = {0}
                                ORDER BY Id", tenantId)
                    .FirstOrDefaultAsync() 
                    ??   throw new InvalidOperationException(
                                "No ArticleCode configuration row found. " +
                                "Please seed the ArticleCodes table with an initial row.");

                articleCode.Increment();

                string generatedCode = articleCode.FormatCode(DateTime.UtcNow.Year);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return generatedCode;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
    }
}