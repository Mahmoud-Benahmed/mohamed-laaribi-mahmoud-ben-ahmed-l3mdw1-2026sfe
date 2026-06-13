using ERP.InvoiceService.Application.DTOs;
using ERP.InvoiceService.Application.Interfaces;
using ERP.InvoiceService.Domain.LocalCache.Client;
using ERP.InvoiceService.Infrastructure.Messaging;
using ERP.InvoiceService.Infrastructure.Messaging.Events;
using ERP.InvoiceService.Infrastructure.Persistence;
using InvoiceService.Application.DTOs;
using InvoiceService.Application.Exceptions;
using InvoiceService.Application.Interfaces;
using InvoiceService.Domain;
using static InvoiceService.Application.Interfaces.IInvoiceRepository;

namespace ERP.InvoiceService.Application.Services.LocalCache.ArticleCache
{
    public class InvoicesService : IInvoicesService
    {
        private readonly IInvoiceRepository _invoiceRepository;
        private readonly IInvoiceNumberGenerator _invoiceNumberGenerator;
        private readonly IClientCacheRepository _clientCacheRepository;
        private readonly IArticleCacheRepository _articleCacheRepository;
        private readonly IEventPublisher _eventPublisher;
        private readonly IStockServiceHttpClient _stockClient;
        private readonly ILogger<InvoicesService> _logger;
        private readonly ITenantContext _tenantContext;


        public InvoicesService(
            ILogger<InvoicesService> logger,
            IStockServiceHttpClient stockClient,
            IInvoiceRepository invoiceRepository,
            IInvoiceNumberGenerator invoiceNumberGenerator,
            IClientCacheRepository clientCacheRepository,
            IArticleCacheRepository articleCacheRepository,
            IEventPublisher eventPublisher,
            ITenantContext tenantContext)
        {
            _logger = logger;
            _stockClient = stockClient;
            _eventPublisher = eventPublisher;
            _clientCacheRepository = clientCacheRepository;
            _invoiceRepository = invoiceRepository;
            _invoiceNumberGenerator = invoiceNumberGenerator;
            _articleCacheRepository = articleCacheRepository;
            _tenantContext = tenantContext;
        }

        public async Task<InvoiceDto> GetByIdAsync(Guid id)
        {
            Invoice invoice = await _invoiceRepository.GetByIdAsync(id) ?? throw new InvoiceNotFoundException(id);

            return invoice.ToDto();
        }
        public async Task<PagedResultDto<InvoiceDto>> GetAllAsync(int pageNumber, int pageSize, bool includeDeleted = false)
        {
            var (invoices, totalCount) = await _invoiceRepository.GetAllAsync(pageNumber, pageSize, includeDeleted);
            var items= invoices.Select(i => i.ToDto()).ToList();
            return new PagedResultDto<InvoiceDto>(
                items,
                totalCount,
                pageNumber,
                pageSize
            );
        }
        public async Task<PagedResultDto<InvoiceDto>> GetByClientIdAsync(Guid clientId, int pageNumber, int pageSize)
        {
            var (invoices, totalCount) = await _invoiceRepository.GetByClientIdAsync(clientId, pageNumber, pageSize);
            var items = invoices.Select(i => i.ToDto()).ToList();
            return new PagedResultDto<InvoiceDto>(items, totalCount, pageNumber, pageSize);
        }
        public async Task<PagedResultDto<InvoiceDto>> GetByStatusAsync(InvoiceStatus status, int pageNumber, int pageSize)
        {
            var (invoices,totalCount) = await _invoiceRepository.GetByStatusAsync(status, pageNumber, pageSize);
            var items = invoices.Select(i => i.ToDto()).ToList();
            return new PagedResultDto<InvoiceDto>(items, totalCount, pageNumber, pageSize);
        }

        // ════════════════════════════════════════════════════════════════════════════
        // CREATE OPERATIONS
        // ════════════════════════════════════════════════════════════════════════════

        public async Task<InvoiceDto> CreateAsync(CreateInvoiceDto dto)
        {
            if (dto.Items == null || !dto.Items.Any())
                throw new InvoiceDomainException("Invoice must have at least one item.");

            if (dto.InvoiceDate > dto.DueDate)
                throw new InvoiceDomainException("Due date cannot be before invoice date.");

            Domain.LocalCache.Client.ClientCache client = await _clientCacheRepository.GetByIdAsync(dto.ClientId) ?? throw new KeyNotFoundException($"Client with Id {dto.ClientId} not found.");

            if (client.IsBlocked)
                throw new ClientBlockedException();

            StockStatusResponse stockStatus = await _stockClient.GetStockStatusAsync();
            ValidateStockAvailability(dto.Items, stockStatus);

            _logger.LogInformation("\n\nCreating invoice for client {ClientId} with {ItemCount} items\n\n", dto.ClientId, dto.Items.Count);
            _logger.LogDebug("\n\nInvoice details: {@InvoiceDto}\n\n", dto);

            decimal discountRate = GetClientDiscountRate(client);


            string invoiceNumber = await _invoiceNumberGenerator.GenerateNextInvoiceNumberAsync(_tenantContext.TenantId);

            // ──── CREATE INVOICE ────
            Invoice invoice = new Invoice(
                invoiceNumber,
                dto.InvoiceDate,
                dto.DueDate,
                dto.TaxMode,
                discountRate,
                client.Id,
                client.Name,
                client.Address,
                dto.AdditionalNotes,
                _tenantContext.TenantId);


            // Load all articles once (single database call)
            List<Guid> articleIds = dto.Items.Select(i => i.ArticleId).Distinct().ToList();
            List<Domain.LocalCache.Article.ArticleCache> articles = await _articleCacheRepository.GetByIdsAsync(articleIds);
            if (articles == null || !articles.Any())
                throw new InvalidOperationException("No articles found for the given IDs.");

            Dictionary<Guid, Domain.LocalCache.Article.ArticleCache> articleDictionary = articles.ToDictionary(a => a.Id, a => a);

            // Now loop through items - no database calls here!
            

            foreach (CreateInvoiceItemDto itemDto in dto.Items)
            {
                if (articleDictionary.TryGetValue(itemDto.ArticleId, out Domain.LocalCache.Article.ArticleCache? article))
                {
                invoice.AddItem(new InvoiceItem(
                    invoice.Id,
                    itemDto.ArticleId,
                    article.Libelle,
                    article.BarCode,
                    itemDto.Quantity,
                    itemDto.UniPriceHT,
                    itemDto.TaxRate));
            }
                else
                {
                    throw new InvalidOperationException($"Article with ID {itemDto.ArticleId} not found");
                }
            }

            invoice.CalculateTotals();

            _logger.LogInformation("\n\nCalculated invoice totals: \nTotalHT={TotalHT}, \nTotalTVA={TotalTVA}, \nTotalTTC={TotalTTC}\n\n", invoice.TotalHT, invoice.TotalTVA, invoice.TotalTTC);

            await CheckClientCreditLimit(client.Id, invoice.TotalTTC);

            await _invoiceRepository.AddAsync(invoice);
            await _invoiceRepository.SaveChangesAsync();


            return invoice.ToDto();
        }

        public async Task<InvoiceDto> UpdateAsync(Guid id, UpdateInvoiceDto dto)
        {
            Invoice? invoice = await _invoiceRepository.GetByIdAsync(id);

            if (invoice is null || invoice.IsDeleted)
                throw new InvoiceNotFoundException(id);

            Domain.LocalCache.Client.ClientCache client = await _clientCacheRepository.GetByIdAsync(dto.ClientId) ?? throw new KeyNotFoundException($"Client with Id {dto.ClientId} not found.");

            if (invoice.Status != InvoiceStatus.DRAFT)
                throw new InvoiceDomainException("Only DRAFT invoices can be updated.");

            StockStatusResponse stockStatus = await _stockClient.GetStockStatusAsync();
            List<CreateInvoiceItemDto> updateDTOs = dto.Items.Select(i => new CreateInvoiceItemDto
            (
                ArticleId: i.ArticleId,
                Quantity: i.Quantity,
                UniPriceHT: i.UniPriceHT,
                TaxRate: i.TaxRate
            )).ToList();
            ValidateStockAvailability(updateDTOs, stockStatus);

            _logger.LogInformation("\n\nUpdating invoice {InvoiceId} for client {ClientId} with {ItemCount} items\n\n", id, dto.ClientId, dto.Items.Count);
            _logger.LogDebug("\n\nInvoice details: {@InvoiceDto}\n\n", dto);

            decimal discountRate = GetClientDiscountRate(client);
            decimal discountMultiplier = 1 - (discountRate / 100);

            _logger.LogInformation("\n\nClient {ClientId} has discount rate of {DiscountRate}%, applying multiplier of {DiscountMultiplier}\n\n", dto.ClientId, discountRate, discountMultiplier);


            // Update header only
            invoice.Update(
                invoiceDate: dto.InvoiceDate,
                dueDate: dto.DueDate,
                taxCalculationMode: dto.TaxMode,
                discountRate: discountRate,
                clientId: dto.ClientId,
                clientFullName: client.Name,
                clientAddress: client.Address,
                additionalNotes: dto.AdditionalNotes
            );

            // Load all articles once (single database call)
            List<Guid> articleIds = dto.Items.Select(i => i.ArticleId).Distinct().ToList();
            List<Domain.LocalCache.Article.ArticleCache> articles = await _articleCacheRepository.GetByIdsAsync(articleIds);
            if (articles == null || !articles.Any())
                throw new InvalidOperationException("No articles found for the given IDs.");

            Dictionary<Guid, Domain.LocalCache.Article.ArticleCache> articleDictionary = articles.ToDictionary(a => a.Id, a => a);

            invoice.ClearItems();
            foreach (UpdateInvoiceItemDto itemDto in dto.Items)
            {
                if (articleDictionary.TryGetValue(itemDto.ArticleId, out Domain.LocalCache.Article.ArticleCache? article))
                {
                    decimal discountedPrice = itemDto.UniPriceHT * discountMultiplier;
                    _logger.LogInformation("\n\nAdding item to invoice: \nArticleId={ArticleId}, \nQuantity={Quantity}, \nUniPriceHT={UniPriceHT}, \nDiscountedPrice={DiscountedPrice}, \nTaxRate={TaxRate}\n\n",
                        itemDto.ArticleId, itemDto.Quantity, itemDto.UniPriceHT, discountedPrice, itemDto.TaxRate);
                    invoice.AddItem(new InvoiceItem(
                        invoice.Id,
                        itemDto.ArticleId,
                        article.Libelle,
                        article.BarCode,
                        itemDto.Quantity,
                        itemDto.UniPriceHT,
                        itemDto.TaxRate));
                }
                else
                {
                    // Handle missing article - log, throw, or skip
                    throw new InvalidOperationException($"Article with ID {itemDto.ArticleId} not found");
                }
            }

            invoice.CalculateTotals();

            _logger.LogInformation("\n\nCalculated invoice totals: \nTotalHT={TotalHT}, \nTotalTVA={TotalTVA}, \nTotalTTC={TotalTTC}\n\n", invoice.TotalHT, invoice.TotalTVA, invoice.TotalTTC);

            await CheckClientCreditLimit(dto.ClientId, invoice.TotalTTC, excludeInvoiceId: id);

            await _invoiceRepository.UpdateAsync(invoice);
            await _invoiceRepository.SaveChangesAsync();

            return invoice.ToDto();
        }

        public async Task<InvoiceDto> AddItemAsync(Guid id, AddInvoiceItemDto dto)
        {
            Invoice? invoice = await _invoiceRepository.GetByIdAsync(id);

            if (invoice is null || invoice.IsDeleted)
                throw new InvoiceNotFoundException(id);

            if (invoice.Status != InvoiceStatus.DRAFT)
                throw new InvoiceDomainException("Items can only be added to DRAFT invoices.");

            decimal invoiceTotalTTC = invoice.TotalTTC + (dto.Quantity * dto.UniPriceHT * (1 + (dto.TaxRate)));

            await CheckClientCreditLimit(invoice.ClientId, invoiceTotalTTC);

            Domain.LocalCache.Article.ArticleCache article = await _articleCacheRepository.GetByIdAsync(dto.ArticleId) ?? throw new KeyNotFoundException($"Article with Id: {dto.ArticleId} not found.");

            InvoiceItem item = new InvoiceItem(
                id,
                dto.ArticleId,
                article.Libelle,
                article.BarCode,
                dto.Quantity,
                dto.UniPriceHT,
                dto.TaxRate);

            invoice.AddItem(item);

            invoice.CalculateTotals();

            await _invoiceRepository.UpdateAsync(invoice);
            await _invoiceRepository.SaveChangesAsync();
            
            return invoice.ToDto();
        }

        public async Task RemoveItemAsync(Guid id, Guid itemId)
        {
            Invoice? invoice = await _invoiceRepository.GetByIdAsync(id);

            if (invoice is null || invoice.IsDeleted)
                throw new InvoiceNotFoundException(id);

            if (invoice.Status != InvoiceStatus.DRAFT)
                throw new InvoiceDomainException("Items can only be removed from DRAFT invoices.");

            invoice.RemoveItem(itemId);

            invoice.CalculateTotals();

            await _invoiceRepository.UpdateAsync(invoice);
            await _invoiceRepository.SaveChangesAsync();
        }

        public async Task<InvoiceDto> FinalizeAsync(Guid id)
        {
            Invoice? invoice = await _invoiceRepository.GetByIdAsync(id);

            if (invoice is null || invoice.IsDeleted)
                throw new InvoiceNotFoundException(id);

            ValidateStockAvailability(
                invoice.Items.Select(i => new CreateInvoiceItemDto
                (
                    ArticleId: i.ArticleId,
                    Quantity: i.Quantity,
                    UniPriceHT: i.UniPriceHT,
                    TaxRate: i.TaxRate
                )).ToList(),
                await _stockClient.GetStockStatusAsync());

            invoice.FinalizeInvoice();


            await _invoiceRepository.UpdateAsync(invoice);
            await _invoiceRepository.SaveChangesAsync();

            // Draft invoices are not published when created with InvoiceStatus.DRAFT in CreateAsync (method above),
            // They are considered created once their Status is UNPAID Meaning that the stock will be effected only by the UNPAID invoices not the DRAFT ones,
            // the draft invoice is peristed in Invoice service as Draft but only sent as UNPAID to StockService & PaymentService so the DRAFT isn't published in CreateAsync,
            // they are published once they are UNPAID so they will be persisted in the StockService for tracking by this statement.
            InvoiceEventDto payload = new InvoiceEventDto(
                Id: invoice.Id,
                InvoiceNumber: invoice.InvoiceNumber,
                TotalTTC: invoice.TotalTTC,
                Status: invoice.Status.ToString(),
                ClientId: invoice.ClientId,
                TenantId: invoice.TenantId,
                Items: invoice.Items.Select(i => new InvoiceItemEventDto(
                    ArticleId: i.ArticleId,
                    Quantity: i.Quantity,
                    UniPriceHT: i.UniPriceHT,
                    TaxRate: i.TaxRate
                )).ToList()
            );
            await _eventPublisher.PublishAsync(InvoiceTopics.Created, payload);

            return invoice.ToDto();
        }

        public async Task<InvoiceDto> MarkAsPaidAsync(Guid id, decimal? paidAmount = null, DateTime? paidAt = null)
        {
            // ──── GET INVOICE ────
            Invoice? invoice = await _invoiceRepository.GetByIdAsync(id);

            if (invoice is null || invoice.IsDeleted)
                throw new InvoiceNotFoundException(id);

            invoice.MarkAsPaid();

            await _invoiceRepository.UpdateAsync(invoice);
            await _invoiceRepository.SaveChangesAsync();

            return invoice.ToDto();
        }

        public async Task MarkAsUnpaidAsync(Guid id)
        {
            // ──── GET INVOICE ────
            Invoice? invoice = await _invoiceRepository.GetByIdAsync(id);

            if (invoice is null || invoice.IsDeleted)
                throw new InvoiceNotFoundException(id);

            invoice.MarkAsUnpaid();
            await _invoiceRepository.UpdateAsync(invoice);
            await _invoiceRepository.SaveChangesAsync();

        }

        public async Task<InvoiceDto> CancelAsync(Guid id)
        {
            Invoice? invoice = await _invoiceRepository.GetByIdAsync(id);

            if (invoice is null || invoice.IsDeleted)
                throw new InvoiceNotFoundException(id);

            invoice.CancelInvoice();

            await _invoiceRepository.UpdateAsync(invoice);
            await _invoiceRepository.SaveChangesAsync();

            InvoiceEventDto payload = new InvoiceEventDto(
                Id: invoice.Id,
                InvoiceNumber: invoice.InvoiceNumber,
                TotalTTC: invoice.TotalTTC,
                Status: invoice.Status.ToString(),
                ClientId: invoice.ClientId,
                TenantId: invoice.TenantId,
                Items: invoice.Items.Select(i => new InvoiceItemEventDto(
                    ArticleId: i.ArticleId,
                    Quantity: i.Quantity,
                    UniPriceHT: i.UniPriceHT,
                    TaxRate: i.TaxRate
                )).ToList()
            );
            await _eventPublisher.PublishAsync(InvoiceTopics.Cancelled, payload);

            return invoice.ToDto();
        }

        // ════════════════════════════════════════════════════════════════════════════
        // SOFT DELETE OPERATIONS
        // ════════════════════════════════════════════════════════════════════════════
        public async Task DeleteAsync(Guid id)
        {
            // ──── GET INVOICE ────
            Invoice invoice = await _invoiceRepository.GetByIdDeletedAsync(id) ?? throw new InvoiceNotFoundException(id);

            invoice.Delete();

            // ──── PERSIST ────
            await _invoiceRepository.UpdateAsync(invoice);
            await _invoiceRepository.SaveChangesAsync();
        }
        public async Task RestoreAsync(Guid id)
        {
            Invoice invoice = await _invoiceRepository.GetByIdAsync(id) ?? throw new InvoiceNotFoundException(id);

            // ──── RESTORE ────
            invoice.Restore();

            // ──── PERSIST ────
            await _invoiceRepository.UpdateAsync(invoice);
            await _invoiceRepository.SaveChangesAsync();
        }


        // =========================
        // STATS
        // =========================
        public async Task<InvoiceStatsDto> GetStatsAsync(int topClientsCount = 5)
        {
            List<IInvoiceRepository.InvoiceStatProjection> projections = (await _invoiceRepository.GetStatsProjectionAsync()).ToList();
            int deletedCount = await _invoiceRepository.GetDeletedCountAsync();

            DateTime now = DateTime.UtcNow;

            // ── Status buckets ───────────────────────────────────────────────────────
            List<IInvoiceRepository.InvoiceStatProjection> drafts = projections.Where(i => i.Status == InvoiceStatus.DRAFT).ToList();
            List<IInvoiceRepository.InvoiceStatProjection> unpaid = projections.Where(i => i.Status == InvoiceStatus.UNPAID).ToList();
            List<IInvoiceRepository.InvoiceStatProjection> paid = projections.Where(i => i.Status == InvoiceStatus.PAID).ToList();
            List<IInvoiceRepository.InvoiceStatProjection> cancelled = projections.Where(i => i.Status == InvoiceStatus.CANCELLED).ToList();
            List<IInvoiceRepository.InvoiceStatProjection> overdue = unpaid.Where(i => i.DueDate < now).ToList();

            // ── Revenue (PAID only) ──────────────────────────────────────────────────
            decimal revenueHT = paid.Sum(i => i.TotalHT);
            decimal revenueTTC = paid.Sum(i => i.TotalTTC);
            decimal tvaColl = paid.Sum(i => i.TotalTVA);

            // ── Outstanding (UNPAID) ─────────────────────────────────────────────────
            decimal outstandingHT = unpaid.Sum(i => i.TotalHT);
            decimal outstandingTTC = unpaid.Sum(i => i.TotalTTC);

            // ── Overdue ──────────────────────────────────────────────────────────────
            decimal overdueHT = overdue.Sum(i => i.TotalHT);
            decimal overdueTTC = overdue.Sum(i => i.TotalTTC);

            // ── Average invoice value (PAID + UNPAID, i.e. real commercial invoices) ─
            List<IInvoiceRepository.InvoiceStatProjection> activeInvoices = paid.Concat(unpaid).ToList();
            decimal avgValueHT = activeInvoices.Count > 0
                ? activeInvoices.Average(i => i.TotalHT)
                : 0m;

            // ── Average days to due (proxy for payment cycle) — PAID invoices only ───
            double avgPaymentDays = paid.Count > 0
                ? paid.Average(i => (i.DueDate - i.InvoiceDate).TotalDays)
                : 0d;

            // ── Top clients by paid revenue TTC ─────────────────────────────────────
            List<ClientRevenueDto> topClients = paid
                .GroupBy(i => new { i.ClientId, i.ClientFullName })
                .Select(g => new ClientRevenueDto
                (
                    ClientId: g.Key.ClientId,
                    ClientFullName: g.Key.ClientFullName,
                    InvoiceCount: g.Count(),
                    RevenueTTC: g.Sum(i => i.TotalTTC)
                ))
                .OrderByDescending(c => c.RevenueTTC)
                .Take(topClientsCount)
                .ToList();

            // ── Monthly breakdown (current calendar year) ────────────────────────────
            int currentYear = now.Year;
            List<IInvoiceRepository.InvoiceStatProjection> yearInvoices = projections
                .Where(i => i.InvoiceDate.Year == currentYear)
                .ToList();

            // O(n) instead
            Dictionary<int, List<InvoiceStatProjection>> byMonth = yearInvoices
                .GroupBy(i => i.InvoiceDate.Month)
                .ToDictionary(g => g.Key, g => g.ToList());

            List<MonthlyStatsDto> monthlyBreakdown = Enumerable.Range(1, 12)
                .Select(month =>
                {
                    List<InvoiceStatProjection> issued = byMonth.GetValueOrDefault(month, []);
                    List<InvoiceStatProjection> monthPaid = issued
                        .Where(i => i.Status == InvoiceStatus.PAID)
                        .ToList();

                    return new MonthlyStatsDto(
                        Year: currentYear,
                        Month: month,
                        IssuedCount: issued.Count,
                        PaidCount: monthPaid.Count,
                        IssuedTTC: issued.Sum(i => i.TotalTTC),
                        PaidTTC: monthPaid.Sum(i => i.TotalTTC)
                    );
                })
                .ToList();

            // ── Assemble ─────────────────────────────────────────────────────────────
            return new InvoiceStatsDto
            (
                TotalInvoices: projections.Count,
                DraftCount: drafts.Count,
                UnpaidCount: unpaid.Count,
                PaidCount: paid.Count,
                CancelledCount: cancelled.Count,
                DeletedCount: deletedCount,
                OverdueCount: overdue.Count,

                TotalRevenueHT: Math.Round(revenueHT, 2),
                TotalRevenueTTC: Math.Round(revenueTTC, 2),
                TotalTVACollected: Math.Round(tvaColl, 2),
                OutstandingHT: Math.Round(outstandingHT, 2),
                OutstandingTTC: Math.Round(outstandingTTC, 2),
                OverdueHT: Math.Round(overdueHT, 2),
                OverdueTTC: Math.Round(overdueTTC, 2),
                AverageInvoiceValueHT: Math.Round(avgValueHT, 2),
                AveragePaymentDays: Math.Round(avgPaymentDays, 1),

                TopClients: topClients.Select(c => c with { RevenueTTC = Math.Round(c.RevenueTTC, 2) }).ToList(),
                MonthlyBreakdown: monthlyBreakdown.Select(m => m with
                {
                    IssuedTTC = Math.Round(m.IssuedTTC, 2),
                    PaidTTC = Math.Round(m.PaidTTC, 2)
                }).ToList()
            );
        }

        private async Task CheckClientCreditLimit(Guid clientId, decimal invoiceTotalTTC, Guid? excludeInvoiceId = null)
        {
            Domain.LocalCache.Client.ClientCache client = await _clientCacheRepository.GetByIdAsync(clientId)
                ?? throw new KeyNotFoundException($"Client with Id: {clientId} not found.");

            // No credit limit set → allow unconditionally
            decimal? effectiveLimit = client.GetEffectiveCreditLimit();
            if (effectiveLimit is null) return;

            IEnumerable<Invoice> invoices = await _invoiceRepository.GetByClientIdAsNoTrackingAsync(clientId);

            decimal clientCurrentCredit = invoices
                .Where(i => i.Status == InvoiceStatus.UNPAID
                            && (excludeInvoiceId == null || i.Id != excludeInvoiceId))
                .Sum(i => i.TotalTTC);

            if (clientCurrentCredit + invoiceTotalTTC > effectiveLimit.Value)
                throw new InvoiceDomainException(
                    $"Cannot create invoice. Client '{client.Name}' exceeds credit limit." +
                    $" Current used: {clientCurrentCredit:F2}, " +
                    $"Attempted: {invoiceTotalTTC:F2}, " +
                    $"Limit: {effectiveLimit.Value:F2}");
        }

        private void ValidateStockAvailability(List<CreateInvoiceItemDto> items, StockStatusResponse stockStatus)
        {
            _logger.LogInformation("IN_STOCK: {Items}",
                string.Join(", ", stockStatus.IN_STOCK.Select(s => $"{s.ArticleId}={s.Quantity}")));
            _logger.LogInformation("OUT_STOCK: {Items}",
                string.Join(", ", stockStatus.OUT_STOCK.Select(s => $"{s.ArticleId}={s.Quantity}")));

            List<string> errors = new List<string>();

            // Create lookup dictionary for O(1) access
            Dictionary<Guid, decimal> inStockLookup = stockStatus.IN_STOCK.ToDictionary(
                s => s.ArticleId,
                s => s.Quantity
            );

            Dictionary<Guid, decimal> outStockLookup = stockStatus.OUT_STOCK.ToDictionary(
                s => s.ArticleId,
                s => s.Quantity
            );

            foreach (CreateInvoiceItemDto item in items)
            {
                decimal availableQuantity = 0;

                // Check if article has positive stock
                if (inStockLookup.TryGetValue(item.ArticleId, out decimal inStock))
                {
                    availableQuantity = inStock;
                }
                // Check if article has negative stock (oversold)
                else if (outStockLookup.TryGetValue(item.ArticleId, out decimal outStock))
                {
                    // Article has negative stock - cannot fulfill
                    errors.Add($"Article {item.ArticleId} has negative stock ({-outStock} units). Cannot fulfill order.");
                    continue;
                }
                // Article not found in any stock (zero stock)
                else
                {
                    availableQuantity = 0;
                }

                // Validate quantity
                if (availableQuantity < item.Quantity)
                {
                    errors.Add($"Article {item.ArticleId} has insufficient stock. " +
                              $"Requested: {item.Quantity}, Available: {availableQuantity}");
                }
            }

            if (errors.Any())
            {
                throw new InvoiceDomainException($"Stock validation failed: {string.Join("; ", errors)}");
            }
        }


        private static decimal GetClientDiscountRate(Domain.LocalCache.Client.ClientCache client)
        {
            if (client.ClientCategories == null || !client.ClientCategories.Any())
                return 0m;

            List<ClientCategoryCache> bulkCategories = client.ClientCategories
                .Where(cc => cc.Category?.UseBulkPricing == true && cc.Category.DiscountRate.HasValue)
                .ToList();

            if (!bulkCategories.Any()) return 0m;

            decimal highest = bulkCategories.Max(cc => cc.Category!.DiscountRate!.Value);

            // DiscountRate is stored as 0.00–1.00 (e.g. 0.15 = 15%) → convert to percentage
            return highest <= 1m ? Math.Round(highest * 100m, 2) : Math.Round(highest, 2);
        }
    }
}