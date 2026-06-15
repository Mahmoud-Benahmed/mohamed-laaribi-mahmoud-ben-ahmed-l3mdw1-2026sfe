using ERP.ArticleService.Application.DTOs;
using ERP.ArticleService.Application.Interfaces;
using ERP.ArticleService.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.ArticleService.Infrastructure.Persistence
{
    public class CategoryRepository : ICategoryRepository
    {
        private readonly ArticleDbContext _context;

        public CategoryRepository(ArticleDbContext context)
        {
            _context = context;
        }

        // =========================
        // CREATE
        // =========================
        public async Task AddAsync(Category category)
        {
            await _context.Categories.AddAsync(category);
        }

        // =========================
        // READ - BY ID
        // =========================
        public async Task<Category?> GetByIdAsync(Guid id)
        {
            return await _context.Categories
                .FirstOrDefaultAsync(c => c.Id == id);
        }

        public async Task<Category?> GetByIdDeletedAsync(Guid id)
        {
            return await _context.Categories.IgnoreQueryFilters().FirstOrDefaultAsync(c => c.Id == id);
        }

        public async Task<(List<Category> Items, int TotalCount)> GetDeletedPagedAsync(
            int pageNumber,
            int pageSize)
        {
            return await PaginationHelper.ToPagedResultAsync(
                _context.Categories.IgnoreQueryFilters().Where(c => c.IsDeleted), pageNumber, pageSize, q => q.OrderBy(c => c.Name));
        }

        public async Task<bool> DuplicateExists(string name, Guid? excludeId = null)
        {
            var query = _context.Categories.Where(c=>
                name.ToLower() == c.Name.ToLower()
            );

            if (excludeId.HasValue)
                query = query.Where(f => f.Id != excludeId.Value);

            return await query.AnyAsync();
        }


        // =========================
        // READ - BY NAME
        // =========================
        public async Task<Category?> GetByNameAsync(string name)
        {
            return await _context.Categories
                .FirstOrDefaultAsync(c => c.Name.ToLower() == name.Trim().ToLower());
        }

        // =========================
        // READ - BY TVA
        // =========================
        public async Task<List<Category>> GetBelowTVAAsync(decimal tva)
        {
            return await _context.Categories
                .Where(c => c.TVA < tva)
                .OrderBy(c => c.TVA)
                .ToListAsync();
        }

        public async Task<List<Category>> GetHigherThanTVAAsync(decimal tva)
        {
            return await _context.Categories
                .Where(c => c.TVA > tva)
                .OrderBy(c => c.TVA)
                .ToListAsync();
        }

        public async Task<List<Category>> GetBetweenTVAAsync(decimal min, decimal max)
        {
            if (min > max)
                throw new ArgumentException("'min' TVA must be less than or equal to 'max' TVA.");

            return await _context.Categories
                .Where(c => c.TVA >= min && c.TVA <= max)
                .OrderBy(c => c.TVA)
                .ToListAsync();
        }

        public async Task<Category?> GetByTVAsync(decimal tva)
        {
            return await _context.Categories
                .FirstOrDefaultAsync(c => c.TVA == tva);
        }


        // =========================
        // READ - ALL
        // =========================
        public async Task<List<Category>> GetAllAsync()
        {
            return await _context.Categories
                .OrderBy(c => c.Name)
                .ToListAsync();
        }

        // =========================
        // DELETE
        // =========================
        public void Remove(Category category)
        {
            _context.Categories.Remove(category);
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
        public async Task<(List<Category> Items, int TotalCount)> GetPagedAsync(
            int pageNumber,
            int pageSize)
        {
            return await PaginationHelper.ToPagedResultAsync(
                _context.Categories, pageNumber, pageSize, q => q.OrderBy(c => c.Name));
        }

        public async Task<(List<Category> Items, int TotalCount)> GetPagedByNameAsync(
            string nameFilter,
            int pageNumber,
            int pageSize)
        {
            IQueryable<Category> query = _context.Categories
                .Where(c => EF.Functions.Like(c.Name, $"%{nameFilter.Trim()}%"));

            return await PaginationHelper.ToPagedResultAsync(
                query, pageNumber, pageSize, q => q.OrderBy(c => c.Name));
        }

        public async Task<(List<Category> Items, int TotalCount)> GetPagedByDateRangeAsync(
            DateTime from,
            DateTime to,
            int pageNumber,
            int pageSize)
        {
            if (from > to)
                throw new ArgumentException("'from' date must be earlier than or equal to 'to' date.");

            IQueryable<Category> query = _context.Categories
                .Where(c => c.CreatedAt >= from && c.CreatedAt <= to);

            return await PaginationHelper.ToPagedResultAsync(
                query, pageNumber, pageSize, q => q.OrderBy(c => c.Name));
        }

        public async Task<CategoryStatsDto> GetStatsAsync()
        {
            // IgnoreQueryFilters to count ALL categories including deleted
            int active = await _context.Categories.CountAsync(_ => true);
            int deleted = await _context.Categories.IgnoreQueryFilters().CountAsync(c => c.IsDeleted);


            return new CategoryStatsDto(active, deleted);
        }
    }
}