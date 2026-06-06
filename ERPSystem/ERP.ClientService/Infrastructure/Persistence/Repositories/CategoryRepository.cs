using ERP.ClientService.Application.DTOs;
using ERP.ClientService.Application.Interfaces;
using ERP.ClientService.Application.Services;
using ERP.ClientService.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;

namespace ERP.ClientService.Infrastructure.Persistence.Repositories;

public class CategoryRepository : ICategoryRepository
{
    private readonly ClientDbContext _context;
    private readonly ITenantContext _tenantContext;

    public CategoryRepository(ClientDbContext context, ITenantContext tenantContext)
    {
        _tenantContext = tenantContext;
        _context = context;
    }

    public async Task AddAsync(Category category) =>
        await _context.Categories.AddAsync(category);

    public Task SaveChangesAsync() =>
        _context.SaveChangesAsync();

    public Task<Category?> GetByIdAsync(Guid id) =>
        _context.Categories.Include(c => c.ClientCategories)
                .ThenInclude(cc => cc.Client)
                .FirstOrDefaultAsync(c => c.Id == id);

    public Task<Category?> GetByIdDeletedAsync(Guid id) =>
        _context.Categories
                .IgnoreQueryFilters()
                .Include(c => c.ClientCategories)
                .ThenInclude(cc => cc.Client)
                .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == _tenantContext.TenantId);

    public Task<Category?> GetByCodeAsync(string code) =>
        _context.Categories
                .FirstOrDefaultAsync(c =>
                    c.Code == code.Trim().ToUpperInvariant());

    public async Task<List<Category>> GetAllAsync() =>
        await _context.Categories
                      .Where(c => c.IsActive)
                      .Include(c => c.ClientCategories)
                        .ThenInclude(cc => cc.Client)
                        .OrderBy(c => c.Name)
                        .ToListAsync();

    public async Task<(List<Category> Items, int TotalCount)> GetAllPagedAsync(
        int pageNumber, int pageSize)
    {
        IQueryable<Category> baseQuery = _context.Categories.OrderBy(c => c.Name);

        int total = await baseQuery.CountAsync();

        List<Category> items = await baseQuery
            .Include(c => c.ClientCategories)
                .ThenInclude(cc => cc.Client)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, total);
    }

    public async Task<(List<Category> Items, int TotalCount)> GetPagedDeletedAsync(
        int pageNumber, int pageSize)
    {
        IQueryable<Category> query = _context.Categories
            .IgnoreQueryFilters()
            .Where(c => c.IsDeleted && c.TenantId == _tenantContext.TenantId)
            .Include(c => c.ClientCategories)
                .ThenInclude(cc => cc.Client);

        int total = await query.CountAsync();

        List<Category> items = await query
            .OrderByDescending(c => c.UpdatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, total);
    }

    public async Task<(List<Category> Items, int TotalCount)> GetPagedByNameAsync(
        string nameFilter, int pageNumber, int pageSize)
    {
        string term = nameFilter.Trim().ToLower();

        IQueryable<Category> baseQuery = _context.Categories
            .Where(c => c.Name.ToLower().Contains(term));

        int total = await baseQuery.CountAsync();

        List<Category> items = await baseQuery
            .Include(c => c.ClientCategories)
                .ThenInclude(cc => cc.Client)
            .OrderBy(c => c.Name)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, total);
    }

    public async Task<List<Category>> GetAllActiveAsync() =>
        await _context.Categories
                      .Where(c => c.IsActive).Include(c => c.ClientCategories).ThenInclude(cc => cc.Client)
                      .OrderBy(c => c.Name)
                      .ToListAsync();

    public async Task<CategoryStatsDto> GetStatsAsync()
    {
        var all = await _context.Categories
            .IgnoreQueryFilters()
            .Where(c => c.TenantId == _tenantContext.TenantId)
            .Select(c => new { c.IsDeleted, c.IsActive })
            .ToListAsync();

        return new CategoryStatsDto(
            TotalCategories: all.Count(c => !c.IsDeleted),
            ActiveCategories: all.Count(c => c.IsActive && !c.IsDeleted),
            InactiveCategories: all.Count(c => !c.IsActive && !c.IsDeleted),
            DeletedCategories: all.Count(c => c.IsDeleted)
        );
    }
}