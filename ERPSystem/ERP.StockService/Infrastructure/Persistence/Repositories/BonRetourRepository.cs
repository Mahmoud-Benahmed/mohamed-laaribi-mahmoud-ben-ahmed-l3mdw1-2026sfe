using ERP.StockService.Application.DTOs;
using ERP.StockService.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Storage;

namespace ERP.StockService.Infrastructure.Persistence.Repositories;

public class BonRetourRepository : IBonRetourRepository
{
    private readonly StockDbContext _context;

    public BonRetourRepository(StockDbContext context) => _context = context;

    // =========================
    // CREATE
    // =========================
    public async Task AddAsync(BonRetour b) => await _context.BonRetours.AddAsync(b);
    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }

    public async Task DeleteByIdAsync(Guid id)
    {
        BonRetour? bon = await _context.BonRetours.FindAsync(id);
        if (bon != null)
        {
            _context.Remove(bon);
            await _context.SaveChangesAsync();
        }
    }

    // =========================
    // READ
    // =========================
    public async Task<BonRetour?> GetByIdAsync(Guid id) =>
        await _context.BonRetours
            .Include(b => b.Lignes)
            .FirstOrDefaultAsync(b => b.Id == id);
    public async Task<BonRetour?> GetByIdForUpdateAsync(Guid id) =>
    await _context.BonRetours
        .Include(b => b.Lignes)
        .FirstOrDefaultAsync(b => b.Id == id);

    public async Task<(List<BonRetour> Items, int TotalCount)> GetAllAsync(int page, int size)
    {
        IIncludableQueryable<BonRetour, IReadOnlyCollection<LigneRetour>> query = _context.BonRetours
            .Include(b => b.Lignes);

        int total = await query.CountAsync();
        List<BonRetour> items = await query
            .OrderByDescending(b => b.CreatedAt)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync();

        return (items, total);
    }

    public async Task<(List<BonRetour> Items, int TotalCount)> GetBySourceIdAsync(Guid sourceId, int page, int size)
    {
        IQueryable<BonRetour> query = _context.BonRetours
            .Include(b => b.Lignes)
            .Where(b => b.SourceId == sourceId);

        int total = await query.CountAsync();
        List<BonRetour> items = await query
            .OrderByDescending(b => b.CreatedAt)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync();

        return (items, total);
    }

    public async Task<(List<BonRetour> Items, int TotalCount)> GetByRetourSourceTypeAsync(RetourSourceType sourceType, int page, int size)
    {
        IQueryable<BonRetour> query = _context.BonRetours
            .Include(b => b.Lignes)
            .Where(b => b.SourceType == sourceType);

        int total = await query.CountAsync();
        List<BonRetour> items = await query
            .OrderByDescending(b => b.CreatedAt)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync();

        return (items, total);
    }

    public Task<(List<BonRetour> Items, int TotalCount)> GetPagedBySourceAsync(Guid sourceId, int page, int size)
    {
        return GetBySourceIdAsync(sourceId, page, size);
    }

    public async Task<(List<BonRetour> Items, int TotalCount)> GetPagedByDateRangeAsync(DateTime from, DateTime to, int page, int size)
    {
        IQueryable<BonRetour> query = _context.BonRetours
            .Include(b => b.Lignes)
            .Where(b => b.CreatedAt.Date >= from.Date && b.CreatedAt.Date <= to.Date);

        int total = await query.CountAsync();
        List<BonRetour> items = await query
            .OrderByDescending(b => b.CreatedAt)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync();

        return (items, total);
    }

    public async Task ReplaceLignesAsync(BonRetour bon, List<LigneRequestDto> newLignes)
    {
        var trackedLignes = _context.ChangeTracker.Entries<LigneRetour>()
            .Where(e => e.Entity.BonRetourId == bon.Id)
            .ToList();
        foreach (var entry in trackedLignes)
            entry.State = EntityState.Detached;

        await _context.LigneRetours
            .IgnoreQueryFilters()
            .Where(l => l.BonRetourId == bon.Id)
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
        int count = await _context.BonRetours.CountAsync();

        return new BonStatsDto(
            TotalCount: count
        );
    }
    public async Task<IDbContextTransaction> BeginTransactionAsync()
            => await _context.Database.BeginTransactionAsync();
}