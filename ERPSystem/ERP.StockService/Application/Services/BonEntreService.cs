using ERP.StockService.Application.DTOs;
using ERP.StockService.Application.Exceptions;
using ERP.StockService.Application.Interfaces;
using ERP.StockService.Domain;
using ERP.StockService.Domain.LocalCache.Article;
using Microsoft.EntityFrameworkCore.Storage;


namespace ERP.StockService.Application.Services;

public class BonEntreService : IBonEntreService
{
    private readonly IBonEntreRepository _repo;
    private readonly IBonNumeroRepository _bonNumberRepo;
    private readonly IJournalStockRepository _journalStockRepository;
    private readonly IFournisseurCacheRepository _fournisseurCacheRepository;
    private readonly IArticleCacheRepository _articleCacheRepository;
    private readonly ITenantContext _tenantContext;

    public BonEntreService(
        IBonEntreRepository repo,
        IArticleCacheRepository articleCacheRepository,
        IBonNumeroRepository bonNumberRepository,
        IJournalStockRepository journalStockRepository,
        IFournisseurCacheRepository fornisseurCacheRepo,
        ITenantContext tenantContext)
    {
        _repo = repo;
        _fournisseurCacheRepository = fornisseurCacheRepo;
        _bonNumberRepo = bonNumberRepository;
        _journalStockRepository = journalStockRepository;
        _articleCacheRepository = articleCacheRepository;
        _tenantContext = tenantContext;
    }

    // =========================
    // CREATE
    // =========================
    public async Task<BonEntreResponseDto> CreateAsync(CreateBonEntreRequestDto dto)
    {
        var fournisseur = await _fournisseurCacheRepository.GetByIdAsync(dto.FournisseurId)
            ?? throw new FournisseurNotFoundException(dto.FournisseurId);

        if(fournisseur.IsBlocked)
            throw new FournisseurBlockedException(dto.FournisseurId);

        if (dto.Lignes is null or { Count: 0 })
            throw new ArgumentException("At least one ligne is required.");

        List<Guid> articleIds = dto.Lignes.Select(l => l.ArticleId).Distinct().ToList();
        List<ArticleCache> articles = await _articleCacheRepository.GetByIdsAsync(articleIds);

        HashSet<Guid> foundIds = articles.Select(a => a.Id).ToHashSet();
        List<Guid> missingIds = articleIds.Where(id => !foundIds.Contains(id)).ToList();
        if (missingIds.Count != 0)
            throw new InvalidOperationException(
                $"Articles not found: {string.Join(", ", missingIds)}");


        await using IDbContextTransaction transaction = await _repo.BeginTransactionAsync();
        try
        {
            string numero = await _bonNumberRepo.GetNextDocumentNumberAsync("BON_ENTRE");
            BonEntre bon = BonEntre.Create(numero, dto.FournisseurId, dto.Observation, _tenantContext.TenantId);

            foreach (LigneRequestDto l in dto.Lignes)
                bon.AddLigne(l.ArticleId, l.Quantity, l.Price);

            bon.ValidateLignes();

            await _repo.AddAsync(bon);
            await _repo.SaveChangesAsync();

            Dictionary<Guid, decimal> stockMap = await _journalStockRepository
                .GetCurrentStocksAsync(bon.Lignes.Select(l => l.ArticleId));

            foreach (LigneEntre ligne in bon.Lignes)
            {
                decimal stockBefore = stockMap.GetValueOrDefault(ligne.ArticleId, 0);

                Console.WriteLine("Bon.TenantID: {0}", bon.TenantId.ToString());

                await _journalStockRepository.AddAsync(JournalStock.Create(
                    articleId: ligne.ArticleId,
                    ligneId: ligne.Id,
                    pieceId: bon.Id,
                    quantity: ligne.Quantity,
                    stockBefore: stockBefore,
                    movementType: StockMovementType.BonEntre,
                    sourceService: "StockService",
                    sourceOperation: "CreateBonEntre",
                    tenantId: bon.TenantId
                ));
            }

            await _journalStockRepository.SaveChangesAsync();
            await transaction.CommitAsync();
            return bon.ToResponseDto();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }

    }

    // =========================
    // UPDATE
    // =========================
    public async Task<BonEntreResponseDto> UpdateAsync(Guid id, UpdateBonEntreRequestDto dto)
    {
        _ = await _fournisseurCacheRepository.GetByIdAsync(dto.FournisseurId)
            ?? throw new KeyNotFoundException($"Fournisseur with Id:{dto.FournisseurId} not found.");

        BonEntre bon = await _repo.GetByIdForUpdateAsync(id)
            ?? throw new BonEntreNotFoundException(id);

        Dictionary<Guid, decimal> oldQtyMap = [];
        Dictionary<Guid, decimal> newQtyMap = [];

        // Only process lines if they were provided
        if (dto.Lignes is not null)
        {
            List<Guid> articleIds = dto.Lignes.Select(l => l.ArticleId).Distinct().ToList();
            List<ArticleCache> articles = await _articleCacheRepository.GetByIdsAsync(articleIds);

            HashSet<Guid> foundIds = articles.Select(a => a.Id).ToHashSet();
            List<Guid> missingIds = articleIds.Where(id => !foundIds.Contains(id)).ToList();
            if (missingIds.Count != 0)
                throw new InvalidOperationException(
                    $"Articles not found: {string.Join(", ", missingIds)}");

            oldQtyMap = bon.Lignes
                        .GroupBy(l => l.ArticleId)
                        .ToDictionary(g => g.Key, g => g.Sum(l => l.Quantity));

            // ✅ ExecuteDelete + clear + re-add, all tracker-safe
            await _repo.ReplaceLignesAsync(bon, dto.Lignes);

            bon.ValidateLignes();
        }

        bon.Update(dto.FournisseurId, dto.Observation);

        await using IDbContextTransaction transaction = await _repo.BeginTransactionAsync();
        try
        {
            await _repo.SaveChangesAsync();

            // Only create journal entries if lines were changed
            if (dto.Lignes is not null)
            {
                // Populate new quantities after SaveChanges
                newQtyMap = bon.Lignes
                    .GroupBy(l => l.ArticleId)
                    .ToDictionary(g => g.Key, g => g.Sum(l => l.Quantity));

                foreach (LigneEntre ligne in bon.Lignes)
                {
                    oldQtyMap.TryGetValue(ligne.ArticleId, out decimal oldQty);
                    decimal delta = ligne.Quantity - oldQty;
                    if (delta == 0) continue;

                    decimal stockBefore = await _journalStockRepository
                        .GetCurrentStockAsync(ligne.ArticleId);

                    Console.WriteLine("Bon.TenantID: {0}", bon.TenantId.ToString());

                    await _journalStockRepository.AddAsync(JournalStock.Create(
                        articleId: ligne.ArticleId,
                        ligneId: ligne.Id,
                        pieceId: bon.Id,
                        quantity: delta,
                        stockBefore: stockBefore,
                        movementType: StockMovementType.BonEntre,
                        sourceService: "StockService",
                        sourceOperation: "UpdateBonEntre",
                    tenantId: bon.TenantId
                    ));
                }

                foreach ((Guid articleId, decimal oldQty) in oldQtyMap)
                {
                    if (newQtyMap.ContainsKey(articleId)) continue;

                    decimal stockBefore = await _journalStockRepository
                        .GetCurrentStockAsync(articleId);

                    await _journalStockRepository.AddAsync(JournalStock.Create(
                        articleId: articleId,
                        ligneId: Guid.Empty,
                        pieceId: bon.Id,
                        quantity: -oldQty,
                        stockBefore: stockBefore,
                        movementType: StockMovementType.BonEntre,
                        sourceService: "StockService",
                        sourceOperation: "UpdateBonEntre_Reversal",
                    tenantId: bon.TenantId
                    ));
                }

                await _journalStockRepository.SaveChangesAsync();
            }

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }

        return bon.ToResponseDto();
    }

    // =========================
    // DELETE
    // =========================
    public async Task DeleteAsync(Guid id)
    {
        BonEntre bon = await _repo.GetByIdAsync(id) ?? throw new BonEntreNotFoundException(id);

        await using IDbContextTransaction transaction = await _repo.BeginTransactionAsync();
        try
        {
            foreach (LigneEntre ligne in bon.Lignes)
            {
                decimal stockBefore = await _journalStockRepository.GetCurrentStockAsync(ligne.ArticleId);
                JournalStock reversal = JournalStock.Create(
                    articleId: ligne.ArticleId,
                    ligneId: ligne.Id,
                    pieceId: bon.Id,
                    quantity: -ligne.Quantity,
                    stockBefore: stockBefore,
                    movementType: StockMovementType.BonEntre,
                    sourceService: "StockService",
                    sourceOperation: "DeleteBonEntre",
                    tenantId: bon.TenantId
                );
                await _journalStockRepository.AddAsync(reversal);
            }
            await _journalStockRepository.SaveChangesAsync();
            await _repo.DeleteByIdAsync(id);
            await _repo.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch { await transaction.RollbackAsync(); throw; }
    }

    // =========================
    // READ
    // =========================
    public async Task<BonEntreResponseDto> GetByIdAsync(Guid id)
    {
        BonEntre bon = await _repo.GetByIdAsync(id) ?? throw new BonEntreNotFoundException(id);
        return bon.ToResponseDto();
    }

    public async Task<PagedResultDto<BonEntreResponseDto>> GetAllAsync(int page, int size)
    {
        ValidatePaging(page, size);
        (List<BonEntre>? items, int total) = await _repo.GetAllAsync(page, size);
        return new PagedResultDto<BonEntreResponseDto>(
            items.Select(b => b.ToResponseDto()).ToList(), total, page, size);
    }

    public async Task<PagedResultDto<BonEntreResponseDto>> GetPagedByFournisseurAsync(
        Guid fournisseurId, int page, int size)
    {
        ValidatePaging(page, size);
        (List<BonEntre>? items, int total) = await _repo.GetByFournisseurAsync(fournisseurId, page, size);
        return new PagedResultDto<BonEntreResponseDto>(
            items.Select(b => b.ToResponseDto()).ToList(), total, page, size);
    }

    public async Task<PagedResultDto<BonEntreResponseDto>> GetPagedByDateRangeAsync(
        DateTime from, DateTime to, int page, int size)
    {
        ValidatePaging(page, size);

        if (from > to)
        {
            (from, to) = (to, from);
        }

        (List<BonEntre>? items, int total) = await _repo.GetPagedByDateRangeAsync(from, to, page, size);
        return new PagedResultDto<BonEntreResponseDto>(
            items.Select(b => b.ToResponseDto()).ToList(), total, page, size);
    }

    public async Task<BonStatsDto> GetStatsAsync()
    {
        return await _repo.GetStatsAsync();
    }

    // =========================
    // HELPERS
    // =========================
    private static void ValidatePaging(int page, int size)
    {
        if (page < 1) throw new ArgumentOutOfRangeException(nameof(page),
            "Page number must be greater than zero.");
        if (size < 1) throw new ArgumentOutOfRangeException(nameof(size),
            "Page size must be greater than zero.");
    }
}