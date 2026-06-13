// IFournisseurRepository
using ERP.StockService.Application.DTOs;
using ERP.StockService.Domain;
using ERP.StockService.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

public interface IBonNumeroRepository
{
    /// <summary>
    /// Gets the next document number for the specified document type.
    /// This method should be called within a transaction to ensure uniqueness.
    /// </summary>
    Task<string> GetNextDocumentNumberAsync(string documentType);

    /// <summary>
    /// Gets the current sequence for a document type (for inspection).
    /// </summary>
    Task<BonNumber?> GetSequenceAsync(string documentType);
}

// IBonEntreRepository
public interface IBonEntreRepository
{
    Task ReplaceLignesAsync(BonEntre bon, List<LigneRequestDto> newLignes);
    Task<BonEntre?> GetByIdForUpdateAsync(Guid id);
    Task AddAsync(BonEntre b);
    Task SaveChangesAsync();
    Task DeleteByIdAsync(Guid id);
    Task<BonEntre?> GetByIdAsync(Guid id);

    Task<(List<BonEntre> Items, int TotalCount)> GetAllAsync(int page, int size);
    Task<(List<BonEntre> Items, int TotalCount)> GetByFournisseurAsync(Guid fournisseurId, int page, int size);
    Task<(List<BonEntre> Items, int TotalCount)> GetPagedByDateRangeAsync(DateTime from, DateTime to, int page, int size);
    Task<BonStatsDto> GetStatsAsync();

    Task<IDbContextTransaction> BeginTransactionAsync();
}

public interface IBonSortieRepository
{
    Task ReplaceLignesAsync(BonSortie bon, List<LigneRequestDto> newLignes);
    Task<BonSortie?> GetByIdForUpdateAsync(Guid id);
    Task AddAsync(BonSortie b);
    Task SaveChangesAsync();
    Task DeleteByIdAsync(Guid id);
    Task<BonSortie?> GetByIdAsync(Guid id);
    Task<(List<BonSortie> Items, int TotalCount)> GetAllAsync(int page, int size);
    Task<(List<BonSortie> Items, int TotalCount)> GetByClientAsync(Guid clientId, int page, int size);
    Task<(List<BonSortie> Items, int TotalCount)> GetPagedByDateRangeAsync(DateTime from, DateTime to, int page, int size);
    Task<(List<BonSortie> Items, int TotalCount)> GetPagedByClientAsync(Guid clientId, int page, int size);
    Task<BonStatsDto> GetStatsAsync();
    Task<IDbContextTransaction> BeginTransactionAsync();
}
public interface IBonRetourRepository
{
    Task ReplaceLignesAsync(BonRetour bon, List<LigneRequestDto> newLignes);
    Task<BonRetour?> GetByIdForUpdateAsync(Guid id);
    Task AddAsync(BonRetour b);
    Task SaveChangesAsync();
    Task DeleteByIdAsync(Guid id);
    Task<BonRetour?> GetByIdAsync(Guid id);
    Task<(List<BonRetour> Items, int TotalCount)> GetAllAsync(int page, int size);
    Task<(List<BonRetour> Items, int TotalCount)> GetBySourceIdAsync(Guid sourceId, int page, int size);
    Task<(List<BonRetour> Items, int TotalCount)> GetByRetourSourceTypeAsync(RetourSourceType sourceType, int page, int size);
    Task<(List<BonRetour> Items, int TotalCount)> GetPagedBySourceAsync(Guid sourceId, int page, int size);
    Task<(List<BonRetour> Items, int TotalCount)> GetPagedByDateRangeAsync(DateTime from, DateTime to, int page, int size);
    Task<BonStatsDto> GetStatsAsync();
    Task<IDbContextTransaction> BeginTransactionAsync();
}

public interface IJournalStockRepository
{
    Task AddAsync(JournalStock entry);
    Task SaveChangesAsync();
    Task<List<JournalStock>> GetByArticleAsync(Guid articleId);
    Task<decimal> GetCurrentStockAsync(Guid articleId);
    Task<Dictionary<string, List<StockItem>>> GetArticlesWithStockAsync();
    Task<Dictionary<Guid, decimal>> GetCurrentStocksAsync(IEnumerable<Guid> articleIds);
}
public interface IInvoiceBonSortieMappingRepository
{
    Task AddAsync(InvoiceBonSortieMapping mapping);
    Task<Guid?> GetBonSortieIdByInvoiceIdAsync(Guid invoiceId);
    Task SaveChangesAsync();
}