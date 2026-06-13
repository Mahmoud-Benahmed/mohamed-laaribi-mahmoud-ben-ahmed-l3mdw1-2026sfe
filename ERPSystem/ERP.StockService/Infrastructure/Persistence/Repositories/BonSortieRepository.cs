using ERP.StockService.Application.DTOs;
using ERP.StockService.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Storage;

namespace ERP.StockService.Infrastructure.Persistence.Repositories;

public class BonSortieRepository : IBonSortieRepository
{
    private readonly StockDbContext _context;

    public BonSortieRepository(StockDbContext context) => _context = context;

    // =========================
    // CREATE / SAVE
    // =========================
    public async Task AddAsync(BonSortie b) => await _context.BonSorties.AddAsync(b);
    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }

    public async Task DeleteByIdAsync(Guid id)
    {
        BonSortie? bon = await _context.BonSorties.FindAsync(id);
        if (bon != null)
        {
            _context.Remove(bon);
            await _context.SaveChangesAsync();
        }
    }

    // =========================
    // READ
    // =========================
    public async Task<BonSortie?> GetByIdAsync(Guid id) =>
        await _context.BonSorties
            .Include(b => b.Lignes)
            .FirstOrDefaultAsync(b => b.Id == id);
    public async Task<BonSortie?> GetByIdForUpdateAsync(Guid id) =>
    await _context.BonSorties
        .Include(b => b.Lignes)
        .FirstOrDefaultAsync(b => b.Id == id);

    public async Task<BonSortie?> GetByIdDeletedAsync(Guid id) =>
        await _context.BonSorties
            .Include(b => b.Lignes)
            .FirstOrDefaultAsync(b => b.Id == id);

    public async Task<(List<BonSortie> Items, int TotalCount)> GetAllAsync(int page, int size)
    {
        IIncludableQueryable<BonSortie, IReadOnlyCollection<LigneSortie>> query = _context.BonSorties
            .Include(b => b.Lignes);

        int total = await query.CountAsync();
        List<BonSortie> items = await query
            .OrderByDescending(b => b.CreatedAt)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync();

        return (items, total);
    }

    public async Task<(List<BonSortie> Items, int TotalCount)> GetPagedByClientAsync(Guid clientId, int page, int size)
    {
        IQueryable<BonSortie> query = _context.BonSorties
            .Include(b => b.Lignes)
            .Where(b => b.ClientId == clientId);

        int total = await query.CountAsync();
        List<BonSortie> items = await query
            .OrderByDescending(b => b.CreatedAt)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync();

        return (items, total);
    }

    public async Task<(List<BonSortie> Items, int TotalCount)> GetPagedByDateRangeAsync(DateTime from, DateTime to, int page, int size)
    {
        IQueryable<BonSortie> query = _context.BonSorties
            .Include(b => b.Lignes)
            .Where(b => b.CreatedAt.Date >= from.Date && b.CreatedAt.Date <= to.Date);

        int total = await query.CountAsync();
        List<BonSortie> items = await query
            .OrderByDescending(b => b.CreatedAt)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync();

        return (items, total);
    }

    public async Task<(List<BonSortie> Items, int TotalCount)> GetByClientAsync(Guid clientId, int page, int size)
    {
        IQueryable<BonSortie> query = _context.BonSorties
            .Include(b => b.Lignes)
            .Where(b => b.ClientId == clientId);

        int total = await query.CountAsync();

        List<BonSortie> items = await query
            .OrderByDescending(b => b.CreatedAt)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync();

        return (items, total);
    }

    public async Task ReplaceLignesAsync(BonSortie bon, List<LigneRequestDto> newLignes)
    {
        var trackedLignes = _context.ChangeTracker.Entries<LigneSortie>()
            .Where(e => e.Entity.BonSortieId == bon.Id)
            .ToList();
        foreach (var entry in trackedLignes)
            entry.State = EntityState.Detached;

        await _context.LigneSorties
            .IgnoreQueryFilters()
            .Where(l => l.BonSortieId == bon.Id)
            .ExecuteDeleteAsync();

        bon.ClearLignes();

        foreach (var l in newLignes)
        {
            var ligne = bon.AddLigne(l.ArticleId, l.Quantity, l.Price);

            // ✅ Force EF to track it as Added — bypasses ValueGeneratedOnAdd confusion
            _context.Entry(ligne).State = EntityState.Added;
        }
    }

    public async Task<BonStatsDto> GetStatsAsync()
    {
        int count = await _context.BonSorties.CountAsync();

        return new BonStatsDto(
            TotalCount: count
        );
    }
    public async Task<IDbContextTransaction> BeginTransactionAsync()
            => await _context.Database.BeginTransactionAsync();
}