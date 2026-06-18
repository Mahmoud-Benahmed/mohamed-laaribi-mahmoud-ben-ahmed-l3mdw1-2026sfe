using ERP.StockService.Application.DTOs;
using ERP.StockService.Application.Exceptions;
using ERP.StockService.Application.Interfaces;
using ERP.StockService.Domain;
using ERP.StockService.Domain.LocalCache.Article;
using ERP.StockService.Domain.LocalCache.Client;
using Microsoft.EntityFrameworkCore.Storage;
using System.Reflection;

namespace ERP.StockService.Application.Services;

public class BonRetourService : IBonRetourService
{
    private readonly IBonRetourRepository _repo;
    private readonly IBonSortieRepository _bonSortieRepo;
    private readonly IBonEntreRepository _bonEntreRepo;
    private readonly IArticleCacheRepository _articleCacheRepository;
    private readonly IClientCacheRepository _clientCacheRepository;
    private readonly IBonNumeroRepository _bonNumeroRepository;
    private readonly IJournalStockRepository _journalStockRepository;
    private readonly ITenantContext _tenantContext;


    public BonRetourService(
        IBonRetourRepository repo,
        IBonSortieRepository bonSortieRepo,
        IBonEntreRepository bonEntreRepo,
        IArticleCacheRepository articleCacheRepository,
        IClientCacheRepository clientCacheRepository,
        IBonNumeroRepository bonNumeroRepository,
        IJournalStockRepository journalStockRepository,
        ITenantContext tenantContext)
    {
        _repo = repo;
        _bonSortieRepo = bonSortieRepo;
        _bonEntreRepo = bonEntreRepo;
        _articleCacheRepository = articleCacheRepository;
        _clientCacheRepository = clientCacheRepository;
        _bonNumeroRepository = bonNumeroRepository;
        _journalStockRepository = journalStockRepository;
        _tenantContext = tenantContext;
    }

    // =========================
    // CREATE
    // =========================
    public async Task<BonRetourResponseDto> CreateAsync(CreateBonRetourRequestDto dto)
    {
        // in case the sourceBon is BonSortie (RetourBonType.BonSortie), check if it exists
        if (dto.SourceType.Equals(RetourSourceType.BonSortie))
        {
            // fetch bonSortie to get its ClientId
            var bonSortie = await _bonSortieRepo.GetByIdAsync(dto.SourceId)
                ?? throw new BonSortieNotFoundException(dto.SourceId); // Better to use specific exception

            // fetch client to check his DelaiRetour
            var client = await _clientCacheRepository.GetByIdAsync(bonSortie.ClientId)
                ?? throw new ClientNotFoundException(bonSortie.ClientId);

            int? delayDays = client.GetEffectiveDelaiRetour();
            if (delayDays.HasValue)
            {
                // Convert days to TimeSpan
                TimeSpan delay = TimeSpan.FromDays(delayDays.Value);

                // Calculate the deadline
                DateTime deadline = bonSortie.CreatedAt.Add(delay);

                // If the current UTC time exceeds the deadline, reject the return
                if (DateTime.UtcNow > deadline)
                {
                    // Use a built-in exception with a meaningful message
                    throw new RetourDelayExceededException(bonSortie.Id, deadline);
                }
            }

        }



        // 1. Validate input
        if (dto.Lignes == null || dto.Lignes.Count == 0)
            throw new ArgumentException("At least one ligne is required.");

        // 2. Resolve source bon and validate party existence
        IReadOnlyList<LigneSource> sourceLignes = dto.SourceType switch
        {
            RetourSourceType.BonSortie => await ResolveBonSortieAsync(dto.SourceId),
            RetourSourceType.BonEntre => await ResolveBonEntreAsync(dto.SourceId),
            _ => throw new ArgumentOutOfRangeException(nameof(dto.SourceType))
        };

        // 3. Begin transaction
        await using IDbContextTransaction transaction = await _repo.BeginTransactionAsync();

        try
        {
            // 4. Generate document number and create BonRetour header
            string numero = await _bonNumeroRepository.GetNextDocumentNumberAsync("BON_RETOUR");
            BonRetour bon = BonRetour.Create(numero, dto.SourceId, dto.SourceType, dto.Motif, dto.Observation, _tenantContext.TenantId);

            // 5. Validate and add each ligne
            foreach (LigneRequestDto ligneDto in dto.Lignes)
            {
                // Verify article exists
                ArticleCache article = await _articleCacheRepository.GetByIdAsync(ligneDto.ArticleId)
                    ?? throw new KeyNotFoundException($"Article with Id {ligneDto.ArticleId} not found");

                // Verify the source bon contains this article and quantity does not exceed original
                LigneSource sourceLigne = sourceLignes.FirstOrDefault(s => s.ArticleId == ligneDto.ArticleId)
                    ?? throw new ArticleNotInSourceBonException(ligneDto.ArticleId, dto.SourceId);

                if (ligneDto.Quantity > sourceLigne.Quantity)
                    throw new RetourQuantityExceedsSourceException(ligneDto.ArticleId, ligneDto.Quantity, sourceLigne.Quantity);

                // Add ligne to BonRetour
                bon.AddLigne(ligneDto.ArticleId, ligneDto.Quantity, ligneDto.Price);
            }

            // 6. Validate all lignes together (e.g., no duplicate articles, etc.)
            bon.ValidateLignes();

            // 7. Persist BonRetour
            await _repo.AddAsync(bon);
            await _repo.SaveChangesAsync();

            // 8. Create journal entries for stock increase
            foreach (LigneRetour ligne in bon.Lignes)
            {
                // Get current stock BEFORE this operation
                decimal stockBefore = await _journalStockRepository.GetCurrentStockAsync(ligne.ArticleId);

                // For a BonRetour, stock INCREASES, so quantity is positive
                JournalStock journal = JournalStock.Create(
                    ligne.ArticleId,
                    ligne.Id,
                    bon.Id,
                    ligne.Quantity,          // Positive quantity = stock increase
                    stockBefore,
                    StockMovementType.BonRetour,
                    "StockService",
                    "CreateBonRetour",
                    tenantId: bon.TenantId
                );

                await _journalStockRepository.AddAsync(journal);
            }
            await _journalStockRepository.SaveChangesAsync();

            // 9. Commit transaction
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
    public async Task<BonRetourResponseDto> UpdateAsync(Guid id, UpdateBonRetourRequestDto dto)
    {
        BonRetour bon = await _repo.GetByIdAsync(id) ?? throw new BonRetourNotFoundException(id);

        // Only allow updating Motif and Observation
        bon.Update(dto.Motif, dto.Observation);

        await _repo.SaveChangesAsync();
        return bon.ToResponseDto();
    }

    // =========================
    // DELETE
    // =========================
    public async Task DeleteAsync(Guid id)
    {
        BonRetour bon = await _repo.GetByIdAsync(id) ?? throw new BonRetourNotFoundException(id);

        await using IDbContextTransaction transaction = await _repo.BeginTransactionAsync();
        try
        {
            // Reverse each journal entry (negative quantity)
            foreach (LigneRetour ligne in bon.Lignes)
            {
                decimal stockBefore = await _journalStockRepository.GetCurrentStockAsync(ligne.ArticleId);
                JournalStock reversal = JournalStock.Create(
                    ligne.ArticleId,
                    ligne.Id,
                    bon.Id,
                    -ligne.Quantity,   // Subtract the returned quantity
                    stockBefore,
                    StockMovementType.BonRetour,
                    "StockService",
                    "DeleteBonRetour",
                    tenantId: bon.TenantId
                );
                await _journalStockRepository.AddAsync(reversal);
            }
            await _journalStockRepository.SaveChangesAsync();

            await _repo.DeleteByIdAsync(id);
            await _repo.SaveChangesAsync();

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }


    // =========================
    // READ
    // =========================
    public async Task<BonRetourResponseDto> GetByIdAsync(Guid id)
    {
        BonRetour bon = await _repo.GetByIdAsync(id) ?? throw new BonRetourNotFoundException(id);
        return bon.ToResponseDto();
    }

    public async Task<PagedResultDto<BonRetourResponseDto>> GetAllAsync(int page, int size)
    {
        ValidatePaging(page, size);
        (List<BonRetour>? items, int total) = await _repo.GetAllAsync(page, size);
        return new PagedResultDto<BonRetourResponseDto>(
            items.Select(b => b.ToResponseDto()).ToList(), total, page, size);
    }

    public async Task<PagedResultDto<BonRetourResponseDto>> GetPagedBySourceAsync(
        Guid sourceId, int page, int size)
    {
        ValidatePaging(page, size);
        (List<BonRetour>? items, int total) = await _repo.GetPagedBySourceAsync(sourceId, page, size);
        return new PagedResultDto<BonRetourResponseDto>(
            items.Select(b => b.ToResponseDto()).ToList(), total, page, size);
    }

    public async Task<PagedResultDto<BonRetourResponseDto>> GetPagedByDateRangeAsync(
        DateTime from, DateTime to, int page, int size)
    {
        ValidatePaging(page, size);
        // Swap dates if needed
        if (from > to)
        {
            (from, to) = (to, from);
        }

        (List<BonRetour>? items, int total) = await _repo.GetPagedByDateRangeAsync(from, to, page, size);
        return new PagedResultDto<BonRetourResponseDto>(
            items.Select(b => b.ToResponseDto()).ToList(), total, page, size);
    }

    // =========================
    // HELPERS
    // =========================
    private async Task<IReadOnlyList<LigneSource>> ResolveBonSortieAsync(Guid sourceId)
    {
        BonSortie bonSortie = await _bonSortieRepo.GetByIdAsync(sourceId)
            ?? throw new BonSortieNotFoundException(sourceId);
        ClientCache client = await _clientCacheRepository.GetByIdAsync(bonSortie.ClientId) ?? throw new KeyNotFoundException($"Client with Id {bonSortie.ClientId} not found");
        return bonSortie.Lignes.Select(l => new LigneSource(l.ArticleId, l.Quantity)).ToList();
    }

    private async Task<IReadOnlyList<LigneSource>> ResolveBonEntreAsync(Guid sourceId)
    {
        BonEntre bonEntre = await _bonEntreRepo.GetByIdAsync(sourceId)
            ?? throw new BonEntreNotFoundException(sourceId);
        return bonEntre.Lignes.Select(l => new LigneSource(l.ArticleId, l.Quantity)).ToList();
    }

    private async Task<IReadOnlyList<LigneSource>> ResolveBonSortieSourceLignesAsync(Guid sourceId)
    {
        BonSortie bonSortie = await _bonSortieRepo.GetByIdAsync(sourceId)
            ?? throw new BonSortieNotFoundException(sourceId);
        return bonSortie.Lignes.Select(l => new LigneSource(l.ArticleId, l.Quantity)).ToList();
    }

    private async Task<IReadOnlyList<LigneSource>> ResolveBonEntreSourceLignesAsync(Guid sourceId)
    {
        BonEntre bonEntre = await _bonEntreRepo.GetByIdAsync(sourceId)
            ?? throw new BonEntreNotFoundException(sourceId);
        return bonEntre.Lignes.Select(l => new LigneSource(l.ArticleId, l.Quantity)).ToList();
    }
    public async Task<BonStatsDto> GetStatsAsync()
    {
        return await _repo.GetStatsAsync();
    }


    private static void ValidatePaging(int page, int size)
    {
        if (page < 1) throw new ArgumentOutOfRangeException(nameof(page),
            "Page number must be greater than zero.");
        if (size < 1) throw new ArgumentOutOfRangeException(nameof(size),
            "Page size must be greater than zero.");
    }

    // Internal projection to avoid domain leakage
    private record LigneSource(Guid ArticleId, decimal Quantity);
}