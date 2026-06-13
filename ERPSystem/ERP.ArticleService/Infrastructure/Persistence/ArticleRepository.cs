using ERP.ArticleService.Application.DTOs;
using ERP.ArticleService.Application.Interfaces;
using ERP.ArticleService.Application.Services;
using ERP.ArticleService.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.ArticleService.Infrastructure.Persistence
{
    public class ArticleRepository : IArticleRepository
    {
        private readonly ArticleDbContext _context;
        private readonly ITenantContext _tenantContext;

        public ArticleRepository(ArticleDbContext context, ITenantContext tenantContext)
        {
            _context = context;
            _tenantContext = tenantContext;
        }

        private IQueryable<Article> BaseQuery() =>
            _context.Articles.Include(a => a.Category);

        // =========================
        // CREATE
        // =========================
        public async Task AddAsync(Article article)
        {
            await _context.Articles.AddAsync(article);
        }

        // =========================
        // READ - BY ID
        // =========================
        public async Task<Article?> GetByIdAsync(Guid id)
        {
            return await BaseQuery()
                .FirstOrDefaultAsync(a => a.Id == id);
        }

        public async Task<bool> ExistsByIdAsync(Guid id)
        {
            return await BaseQuery()
                .AnyAsync(a => a.Id == id);
        }

        public async Task<bool> ExistsForCategoryAsync(Guid categoryId)
        {
            return await _context.Articles
                .AnyAsync(a => a.CategoryId == categoryId);
        }

        public async Task<Article?> GetByIdDeletedAsync(Guid id)
        {
            return await BaseQuery()
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == _tenantContext.TenantId);
        }

        public async Task<bool> DuplicateExists(string code, Guid? excludeId = null)
        {
            var query = BaseQuery().Where(a =>
                a.BarCode.ToLower() == code.ToLower() ||
                a.CodeRef.ToLower() == code.ToLower()
            );

            if (excludeId.HasValue)
                query = query.Where(a => a.Id != excludeId.Value);

            return await query.AnyAsync();
        }

        // =========================
        // READ - BY CODE
        // =========================
        public async Task<Article?> GetByCodeAsync(string code)
        {
            return await BaseQuery()
                .FirstOrDefaultAsync(a => a.CodeRef == code || a.BarCode == code);
        }

        // =========================
        // READ - BY BARCODE
        // =========================
        public async Task<Article?> GetByBarCodeAsync(string barCode)
        {
            return await BaseQuery()
                .FirstOrDefaultAsync(a => a.BarCode == barCode);
        }

        // =========================
        // SAVE CHANGES
        // =========================
        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }

        // =========================
        // PAGING / FILTERING
        // =========================
        public async Task<(List<Article> Items, int TotalCount)> GetAllAsync(int pageNumber, int pageSize)
        {
            // HasQueryFilter handles !IsDeleted automatically
            IQueryable<Article> query = BaseQuery();
            return await PaginationHelper.ToPagedResultAsync(
                query, pageNumber, pageSize, q => q.OrderBy(a => a.CreatedAt));
        }

        public async Task<(List<Article> Items, int TotalCount)> GetPagedByCategoryIdAsync(Guid categoryId, int pageNumber, int pageSize)
        {
            // HasQueryFilter handles !IsDeleted automatically
            IQueryable<Article> query = BaseQuery().Where(a => a.CategoryId == categoryId);
            return await PaginationHelper.ToPagedResultAsync(
                query, pageNumber, pageSize, q => q.OrderBy(a => a.CreatedAt));
        }

        public async Task<(List<Article> Items, int TotalCount)> GetPagedByLibelleAsync(string libelleFilter, int pageNumber, int pageSize)
        {
            // HasQueryFilter handles !IsDeleted automatically
            IQueryable<Article> query = BaseQuery().Where(a => EF.Functions.Like(a.Libelle, $"%{libelleFilter.Trim()}%"));
            return await PaginationHelper.ToPagedResultAsync(
                query, pageNumber, pageSize, q => q.OrderBy(a => a.Libelle));
        }

        public async Task<(List<Article> Items, int TotalCount)> GetPagedDeletedAsync(int pageNumber, int pageSize)
        {
            // IgnoreQueryFilters to bypass HasQueryFilter, then filter deleted only
            IQueryable<Article> query = BaseQuery()
                .IgnoreQueryFilters()
                .Where(a => a.IsDeleted && a.TenantId == _tenantContext.TenantId);
            return await PaginationHelper.ToPagedResultAsync(
                query, pageNumber, pageSize, q => q.OrderBy(a => a.CreatedAt));
        }

        // =========================
        // STATS
        // =========================
        public async Task<ArticleStatsDto> GetStatsAsync()
        {
            int total = await _context.Articles.IgnoreQueryFilters().Where(a=> a.TenantId == _tenantContext.TenantId).CountAsync();
            int active = await _context.Articles.CountAsync();
            int deleted = await _context.Articles.IgnoreQueryFilters().CountAsync(a => a.IsDeleted && a.TenantId == _tenantContext.TenantId);
            int categoriesCount = await _context.Categories.CountAsync();

            return new ArticleStatsDto(
                TotalCount: total,
                ActiveCount: active,
                DeletedCount: deleted,
                CategoriesCount: categoriesCount
            );
        }
    }
}