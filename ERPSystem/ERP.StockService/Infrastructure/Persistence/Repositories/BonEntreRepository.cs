using ERP.StockService.Application.DTOs;
using ERP.StockService.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace ERP.StockService.Infrastructure.Persistence.Repositories
{
    public class BonEntreRepository : IBonEntreRepository
    {
        private readonly StockDbContext _context;

        public BonEntreRepository(StockDbContext context) => _context = context;

        public async Task AddAsync(BonEntre b) => await _context.BonEntres.AddAsync(b);
        public async Task SaveChangesAsync()
        {
            foreach (var entry in _context.ChangeTracker.Entries())
            {
                Console.WriteLine($"[ChangeTracker] {entry.Entity.GetType().Name} | Id: {entry.Property("Id").CurrentValue} | State: {entry.State}");
            }
            await _context.SaveChangesAsync();
        }

        // ── base query helpers ────────────────────────────────────────────────
        // FIX: Include Fournisseur with IgnoreQueryFilters so that BonEntres
        // whose Fournisseur has been soft-deleted are still returned correctly.
        // The BonEntre-level query filter (!IsDeleted) is still applied by EF
        // automatically; we only bypass it for the Fournisseur navigation.
        private IQueryable<BonEntre> BaseQuery() =>
            _context.BonEntres
                .Include(b => b.Lignes)
                .AsSplitQuery();

        // ── single record ─────────────────────────────────────────────────────
        public async Task<BonEntre?> GetByIdAsync(Guid id) =>
            await BaseQuery()
                .FirstOrDefaultAsync(b => b.Id == id);

        // Repository
        public async Task<BonEntre?> GetByIdForUpdateAsync(Guid id)
        {
            var bon= await _context.BonEntres
                .Include(b => b.Lignes)
                .FirstOrDefaultAsync(b => b.Id == id);

            Console.WriteLine($"[GetByIdForUpdate] BonEntre: {bon?.Id}, Lignes loaded: {bon?.Lignes.Count}");

            return bon;
            
        }

        public async Task<BonEntre?> GetByIdDeletedAsync(Guid id) =>
            await _context.BonEntres
                .IgnoreQueryFilters()
                .Include(b => b.Lignes)
                .AsSplitQuery()
                .FirstOrDefaultAsync(b => b.Id == id);

        // ── list queries ──────────────────────────────────────────────────────
        public async Task<(List<BonEntre> Items, int TotalCount)> GetAllAsync(int page, int size)
        {
            IOrderedQueryable<BonEntre> query = BaseQuery().OrderByDescending(b => b.CreatedAt);
            int total = await query.CountAsync();
            List<BonEntre> items = await query.Skip((page - 1) * size).Take(size).ToListAsync();
            return (items, total);
        }

        public async Task<(List<BonEntre> Items, int TotalCount)> GetByFournisseurAsync(
            Guid fournisseurId, int page, int size)
        {
            IOrderedQueryable<BonEntre> query = BaseQuery()
                .Where(b => b.FournisseurId == fournisseurId)
                .OrderByDescending(b => b.CreatedAt);
            int total = await query.CountAsync();
            List<BonEntre> items = await query.Skip((page - 1) * size).Take(size).ToListAsync();
            return (items, total);
        }

        public async Task<(List<BonEntre> Items, int TotalCount)> GetPagedDeletedAsync(
            int page, int size)
        {
            IOrderedQueryable<BonEntre> query = _context.BonEntres
                .IgnoreQueryFilters()
                .Include(b => b.Lignes)
                .AsSplitQuery()
                .OrderByDescending(b => b.CreatedAt);

            int total = await query.CountAsync();
            List<BonEntre> items = await query
                .Skip((page - 1) * size)
                .Take(size)
                .ToListAsync();

            return (items, total);
        }

        public async Task<(List<BonEntre> Items, int TotalCount)> GetPagedByDateRangeAsync(
            DateTime from, DateTime to, int page, int size)
        {
            IOrderedQueryable<BonEntre> query = BaseQuery()
                .Where(b => b.CreatedAt.Date >= from.Date && b.CreatedAt.Date <= to.Date)
                .OrderByDescending(b => b.CreatedAt);
            int total = await query.CountAsync();
            List<BonEntre> items = await query.Skip((page - 1) * size).Take(size).ToListAsync();
            return (items, total);
        }

        public async Task DeleteByIdAsync(Guid id)
        {
            BonEntre? bon = await _context.BonEntres.FindAsync(id);
            if (bon != null)
            {
                _context.Remove(bon);
                await _context.SaveChangesAsync();
            }
        }
        public async Task ReplaceLignesAsync(BonEntre bon, List<LigneRequestDto> newLignes)
        {
            var trackedLignes = _context.ChangeTracker.Entries<LigneEntre>()
                .Where(e => e.Entity.BonEntreId == bon.Id)
                .ToList();
            foreach (var entry in trackedLignes)
                entry.State = EntityState.Detached;

            await _context.LigneEntres
                .IgnoreQueryFilters()
                .Where(l => l.BonEntreId == bon.Id)
                .ExecuteDeleteAsync();

            bon.ClearLignes();

            foreach (var l in newLignes)
            {
                var ligne = bon.AddLigne(l.ArticleId, l.Quantity, l.Price);

                // ✅ Force EF to track it as Added — bypasses ValueGeneratedOnAdd confusion
                _context.Entry(ligne).State = EntityState.Added;
            }
        }

        // ── stats ─────────────────────────────────────────────────────────────
        public async Task<BonStatsDto> GetStatsAsync()
        {
            int count = await _context.BonEntres.CountAsync();

            return new BonStatsDto(
                TotalCount: count
            );
        }

        public async Task<IDbContextTransaction> BeginTransactionAsync()
            => await _context.Database.BeginTransactionAsync();
    }
}