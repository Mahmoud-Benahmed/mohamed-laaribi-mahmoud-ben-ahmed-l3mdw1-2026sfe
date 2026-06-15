using ERP.StockService.Application.DTOs;
using ERP.StockService.Application.Interfaces;
using ERP.StockService.Domain;

namespace ERP.StockService.Application.Services.LocalCache.InvoiceCache
{
    public class InvoiceCacheService : IInvoiceCacheService
    {
        private readonly IJournalStockRepository _journalStockRepo;
        private readonly IBonRetourService _bonRetourService;
        private readonly IBonSortieService _bonSortieService;
        private readonly IInvoiceBonSortieMappingRepository _invoiceBonSortieMappingRepo;
        private readonly ITenantContext _tenantContext;

        public InvoiceCacheService(
                                    IJournalStockRepository journalStockRepo,
                                    IBonRetourService bonRetourService,
                                    IBonSortieService bonSortieService,
                                    IInvoiceBonSortieMappingRepository invoiceBonSortieMappingRepo,
                                    ITenantContext tenantContext)
        {
            _journalStockRepo = journalStockRepo;
            _bonRetourService = bonRetourService;
            _bonSortieService = bonSortieService;
            _invoiceBonSortieMappingRepo = invoiceBonSortieMappingRepo;
            _tenantContext = tenantContext;
        }

        public async Task SyncCreatedAsync(InvoiceDto invoiceDto)
        {
            if (invoiceDto.Items == null || invoiceDto.Items.Count == 0)
                throw new ArgumentException("Invoice has no items to sync.");

            IEnumerable<Guid> articleIds = invoiceDto.Items.Select(i => i.ArticleId).Distinct();
            Dictionary<Guid, decimal> stockMap = await _journalStockRepo.GetCurrentStocksAsync(articleIds);

            // Create BonSortie to record stock going out
            CreateBonSortieRequestDto createBonSortieDto = new CreateBonSortieRequestDto(
                ClientId: invoiceDto.ClientId,
                Observation: $"Auto-generated from Invoice {invoiceDto.InvoiceNumber}",
                Lignes: invoiceDto.Items.Select(i => new LigneRequestDto(
                    ArticleId: i.ArticleId,
                    Quantity: i.Quantity,
                    Price: i.UniPriceHT
                )).ToList()
            );

            BonSortieResponseDto bonSortie = await _bonSortieService.CreateAsync(createBonSortieDto);
            InvoiceBonSortieMapping mapping = new InvoiceBonSortieMapping(invoiceDto.Id, bonSortie.Id, _tenantContext.TenantId);
            await _invoiceBonSortieMappingRepo.AddAsync(mapping);
            await _invoiceBonSortieMappingRepo.SaveChangesAsync();
        }

        public async Task SyncCancelledAsync(InvoiceDto invoiceDto)
        {
            if (invoiceDto.Items == null || invoiceDto.Items.Count == 0)
                throw new ArgumentException("Invoice has no items to sync.");

            Guid? bonSortieId = await _invoiceBonSortieMappingRepo.GetBonSortieIdByInvoiceIdAsync(invoiceDto.Id);

            if (bonSortieId == null)
                throw new InvalidOperationException($"No BonSortie found for invoice {invoiceDto.Id}");

            // Create BonRetour to reverse the stock movement
            CreateBonRetourRequestDto createBonRetourDto = new CreateBonRetourRequestDto(
                SourceId: bonSortieId.Value,
                SourceType: RetourSourceType.BonSortie,
                Motif: $"Invoice {invoiceDto.InvoiceNumber} cancelled",
                Observation: $"Auto-reversal from Invoice cancellation",
                Lignes: invoiceDto.Items.Select(i => new LigneRequestDto(
                    ArticleId: i.ArticleId,
                    Quantity: i.Quantity,
                    Price: i.UniPriceHT
                )).ToList()
            );

            await _bonRetourService.CreateAsync(createBonRetourDto);
        }
    }
}
