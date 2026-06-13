using ERP.ClientService.Application.DTOs;
using ERP.ClientService.Application.Interfaces;
using ERP.ClientService.Application.Services;
using ERP.ClientService.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.ClientService.Infrastructure.Persistence.Repositories;

public class ClientRepository : IClientRepository
{
    private readonly ClientDbContext _context;
    private readonly ITenantContext _tenantContext;

    public ClientRepository(ClientDbContext context, ITenantContext tenantContext)
    {
        _context = context;
        _tenantContext = tenantContext;
    }

    public async Task AddAsync(Client client) =>
        await _context.Clients.AddAsync(client);

    public Task SaveChangesAsync() =>
        _context.SaveChangesAsync();

    public Task<Client?> GetByIdAsync(Guid id) =>
        _context.Clients
                .Include(c => c.ClientCategories)
                    .ThenInclude(cc => cc.Category)
                .FirstOrDefaultAsync(c => c.Id == id);

    public Task<Client?> GetByIdDeletedAsync(Guid id) =>
        _context.Clients
                .IgnoreQueryFilters()
                .Include(c => c.ClientCategories)
                    .ThenInclude(cc => cc.Category)
                .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == _tenantContext.TenantId);

    public Task<Client?> GetByEmailAsync(string email) =>
        _context.Clients
                .FirstOrDefaultAsync(c =>
                    c.Email == email.Trim().ToLowerInvariant());
    public async Task<bool> DuplicateExists(string email, string phone, Guid? excludeId = null)
    {
        var query = _context.Clients.Where(c=>
            c.Email.ToLower() == email.ToLower() ||
            (!string.IsNullOrEmpty(c.Phone) && c.Phone == phone)
        );

        if (excludeId.HasValue)
            query = query.Where(c => c.Id != excludeId);

        return await query.AnyAsync();
    }

    public async Task<(List<Client> Items, int TotalCount)> GetAllAsync(
        int pageNumber, int pageSize)
    {
        IOrderedQueryable<Client> query = _context.Clients.OrderBy(c => c.Name);

        int total = await query.CountAsync();
        List<Client> items = await query
            .Include(c => c.ClientCategories)
                .ThenInclude(cc => cc.Category)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, total);
    }
    public async Task<(List<Client> Items, int TotalCount)> GetPagedByCategoryIdAsync(
        Guid categoryId, int pageNumber, int pageSize)
    {
        IOrderedQueryable<Client> query = _context.Clients
                            .Where(c => c.ClientCategories
                                .Any(cc => cc.CategoryId == categoryId))
                            .OrderBy(c => c.Name);

        int total = await query.CountAsync();
        List<Client> items = await query
            .Include(c => c.ClientCategories)
                .ThenInclude(cc => cc.Category)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, total);
    }

    public async Task<(List<Client> Items, int TotalCount)> GetPagedDeletedAsync(
        int pageNumber, int pageSize)
    {
        IOrderedQueryable<Client> query = _context.Clients
                            .IgnoreQueryFilters()
                            .Where(c => c.IsDeleted && c.TenantId == _tenantContext.TenantId)
                            .OrderByDescending(c => c.UpdatedAt);

        int total = await query.CountAsync();
        List<Client> items = await query
            .Include(c => c.ClientCategories)
                .ThenInclude(cc => cc.Category)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, total);
    }

    public async Task<(List<Client> Items, int TotalCount)> GetPagedByNameAsync(
        string nameFilter, int pageNumber, int pageSize)
    {
        string term = nameFilter.Trim().ToLower();
        IOrderedQueryable<Client> query = _context.Clients
                            .Where(c => c.Name.ToLower().Contains(term))
                            .OrderBy(c => c.Name);

        int total = await query.CountAsync();
        List<Client> items = await query
            .Include(c => c.ClientCategories)
                .ThenInclude(cc => cc.Category)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, total);
    }

    public async Task<ClientStatsDto> GetStatsAsync()
    {
        var counts = await _context.Clients
            .IgnoreQueryFilters()
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Total = g.Count(c => !c.IsDeleted && c.TenantId == _tenantContext.TenantId),
                Active = g.Count(c => !c.IsBlocked && !c.IsDeleted && c.TenantId == _tenantContext.TenantId),
                Blocked = g.Count(c => c.IsBlocked && !c.IsDeleted && c.TenantId == _tenantContext.TenantId),
                Deleted = g.Count(c => c.IsDeleted && c.TenantId == _tenantContext.TenantId),
            })
            .FirstOrDefaultAsync();

        var perCategory = await _context.Categories
            .Select(c => new
            {
                c.Id,
                c.Name,
                ClientCount = c.ClientCategories.Count()
            })
            .OrderByDescending(x => x.ClientCount)
            .ToListAsync();

        List<CategoryClientCountDto> perCategoryDto = perCategory
            .Select(x => new CategoryClientCountDto(x.Id, x.Name, x.ClientCount))
            .ToList();

        return counts is null
            ? new ClientStatsDto(0, 0, 0, 0, perCategoryDto)
            : new ClientStatsDto(
                counts.Total,
                counts.Active,
                counts.Blocked,
                counts.Deleted,
                perCategoryDto);
    }
}