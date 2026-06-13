using ERP.FournisseurService.Application.DTOs;
using ERP.FournisseurService.Application.Interfaces;
using ERP.FournisseurService.Application.Services;
using ERP.FournisseurService.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.FournisseurService.Infrastructure.Persistence.Repositories;

public class FournisseurRepository : IFournisseurRepository
{
    private readonly FournisseurDbContext _context;
    private readonly ITenantContext _tenantContext;

    public FournisseurRepository(FournisseurDbContext context, ITenantContext tenantContext)
    {
        _tenantContext = tenantContext;
        _context = context;
    }
    // =========================
    // CREATE / SAVE
    // =========================
    public async Task AddAsync(Fournisseur f) => await _context.Fournisseurs.AddAsync(f);
    public async Task SaveChangesAsync() => await _context.SaveChangesAsync();

    // =========================
    // READ BY ID
    // =========================
    public async Task<Fournisseur?> GetByIdAsync(Guid id) =>
        await _context.Fournisseurs.FirstOrDefaultAsync(f => f.Id == id && !f.IsDeleted);

    public async Task<bool> DuplicateExists(string email, string taxNum, string rib, Guid? excludeId = null)
    {
        var query = _context.Fournisseurs.Where(f =>
            (!string.IsNullOrEmpty(rib) && f.RIB.ToLower() == rib.ToLower())
            || (!string.IsNullOrEmpty(email) && !string.IsNullOrEmpty(f.Email) && f.Email.ToLower() == email.ToLower())
            || (!string.IsNullOrEmpty(taxNum) && !string.IsNullOrEmpty(f.TaxNumber) && f.TaxNumber.ToLower() == taxNum.ToLower())
        );

        if (excludeId.HasValue)
            query = query.Where(f => f.Id != excludeId.Value);

        return await query.AnyAsync();
    }

    public async Task<Fournisseur?> GetByIdDeletedAsync(Guid id) =>
        await _context.Fournisseurs.IgnoreQueryFilters().FirstOrDefaultAsync(f => f.Id == id && f.IsDeleted && f.TenantId == _tenantContext.TenantId);

    // =========================
    // PAGING
    // =========================
    public async Task<(List<Fournisseur> Items, int TotalCount)> GetAllAsync(int page, int size)
    {
        ValidatePaging(page, size);

        IQueryable<Fournisseur> query = _context.Fournisseurs.Where(f => !f.IsDeleted);

        int total = await query.CountAsync();
        List<Fournisseur> items = await query
            .OrderBy(f => f.Name)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync();

        return (items, total);
    }

    public async Task<(List<Fournisseur> Items, int TotalCount)> GetPagedDeletedAsync(int page, int size)
    {
        ValidatePaging(page, size);

        IQueryable<Fournisseur> query = _context.Fournisseurs.IgnoreQueryFilters().Where(f => f.IsDeleted && f.TenantId == _tenantContext.TenantId);

        int total = await query.CountAsync();
        List<Fournisseur> items = await query
            .OrderBy(f => f.Name)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync();

        return (items, total);
    }

    public async Task<(List<Fournisseur> Items, int TotalCount)> GetPagedByNameAsync(
    string nameFilter, int page, int size)
    {
        IQueryable<Fournisseur> query = _context.Fournisseurs
            .Where(f => !f.IsDeleted && f.Name.Contains(nameFilter.Trim()));

        int total = await query.CountAsync();

        List<Fournisseur> items = await query
            .OrderBy(f => f.Name)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync();

        return (items, total);
    }

    // =========================
    // STATS
    // =========================
    public async Task<FournisseurStatsDto> GetStatsAsync()
    {
        var counts = await _context.Fournisseurs.IgnoreQueryFilters()
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Total = g.Count(f=> !f.IsDeleted && f.TenantId == _tenantContext.TenantId),
                Blocked = g.Count(f => f.IsBlocked && !f.IsDeleted && f.TenantId == _tenantContext.TenantId),
                Deleted = g.Count(f => f.IsDeleted && f.TenantId == _tenantContext.TenantId),
            })
            .FirstOrDefaultAsync();

        return new FournisseurStatsDto(
            TotalFournisseurs: counts?.Total ?? 0,
            ActiveFournisseurs: (counts?.Total ?? 0) - (counts?.Blocked ?? 0) - (counts?.Deleted ?? 0),
            BlockedFournisseurs: counts?.Blocked ?? 0,
            DeletedFournisseurs: counts?.Deleted ?? 0
        );
    }

    // =========================
    // HELPERS
    // =========================
    private static void ValidatePaging(int page, int size)
    {
        if (page < 1) throw new ArgumentOutOfRangeException(nameof(page), "Page number must be greater than zero.");
        if (size < 1) throw new ArgumentOutOfRangeException(nameof(size), "Page size must be greater than zero.");
    }
}