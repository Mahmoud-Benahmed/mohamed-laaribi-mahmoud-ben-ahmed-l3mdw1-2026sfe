using ERP.InvoiceService.Application.DTOs;
using ERP.InvoiceService.Application.Interfaces;
using ERP.InvoiceService.Properties;
using InvoiceService.Application.DTOs;
using InvoiceService.Application.Interfaces;
using InvoiceService.Domain;
using InvoiceService.Services;
using Microsoft.AspNetCore.Mvc;

namespace InvoiceService.API.Controllers
{
    [ApiController]
    public class InvoicesController : ControllerBase
    {
        private readonly IInvoicesService _invoiceService;
        private readonly IInvoicePdfGenerator _pdfGenerator;
        private readonly IArticleCacheService _articleCacheService;
        private readonly IClientCacheService _clientcacheService;
        public InvoicesController(IInvoicesService invoiceService,
                                IInvoicePdfGenerator pdfGenerator,
                                IArticleCacheService articleCacheService,
                                IClientCacheService clientcacheService)
        {
            _invoiceService = invoiceService;
            _pdfGenerator = pdfGenerator;
            _articleCacheService = articleCacheService;
            _clientcacheService = clientcacheService;
        }
        // ════════════════════════════════════════════════════════════════════════════
        // GET OPERATIONS
        // ════════════════════════════════════════════════════════════════════════════
        [HttpGet(ApiRoutes.Invoices.GetAll)]
        public async Task<IActionResult> GetAll([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10, 
                                                [FromQuery] bool includeDeleted = false)
        {
            PagedResultDto<InvoiceDto> invoices = await _invoiceService.GetAllAsync(pageNumber, pageSize, includeDeleted);
            return Ok(invoices);
        }

        [HttpGet(ApiRoutes.Invoices.GetById)]
        public async Task<IActionResult> GetById([FromRoute] Guid id)
        {
            InvoiceDto invoice = await _invoiceService.GetByIdAsync(id);
            return Ok(invoice);
        }

        [HttpGet(ApiRoutes.Invoices.GetByClient)]
        public async Task<IActionResult> GetByClient([FromRoute] Guid clientId, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
        {
            PagedResultDto<InvoiceDto> invoices = await _invoiceService.GetByClientIdAsync(clientId, pageNumber, pageSize);
            return Ok(invoices);
        }

        [HttpGet(ApiRoutes.Invoices.GetByStatus)]
        public async Task<IActionResult> GetByStatus([FromRoute] string status, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
        {
            if (!Enum.TryParse<InvoiceStatus>(status, ignoreCase: true, out InvoiceStatus invoiceStatus))
                return BadRequest($"Invalid status value: '{status}'. Valid values: DRAFT, UNPAID, PAID, CANCELLED");

            PagedResultDto<InvoiceDto> invoices = await _invoiceService.GetByStatusAsync(invoiceStatus, pageNumber, pageSize);
            return Ok(invoices);
        }

        // ════════════════════════════════════════════════════════════════════════════
        // POST OPERATIONS - Create/Add
        // ════════════════════════════════════════════════════════════════════════════
        [HttpPost(ApiRoutes.Invoices.Create)]
        public async Task<IActionResult> Create([FromBody] CreateInvoiceDto dto)
        {
            InvoiceDto invoice = await _invoiceService.CreateAsync(dto);
            return CreatedAtAction(
                nameof(GetById),
                new { id = invoice.Id },
                invoice);
        }

        [HttpPost(ApiRoutes.Invoices.AddItem)]
        public async Task<IActionResult> AddItem([FromRoute] Guid id, [FromBody] AddInvoiceItemDto dto)
        {
            InvoiceDto invoice = await _invoiceService.AddItemAsync(id, dto);
            return Ok(invoice);
        }

        // ════════════════════════════════════════════════════════════════════════════
        // POST OPERATIONS - Create/Add
        // ════════════════════════════════════════════════════════════════════════════
        [HttpPut(ApiRoutes.Invoices.Update)]
        public async Task<IActionResult> Update([FromRoute] Guid id, [FromBody] UpdateInvoiceDto dto)
        {
            InvoiceDto invoice = await _invoiceService.UpdateAsync(id, dto);
            return Ok(invoice);
        }

        // ════════════════════════════════════════════════════════════════════════════
        // DELETE OPERATIONS
        // ════════════════════════════════════════════════════════════════════════════
        [HttpDelete(ApiRoutes.Invoices.RemoveItem)]
        public async Task<IActionResult> RemoveItem([FromRoute] Guid id, [FromRoute] Guid itemId)
        {
            await _invoiceService.RemoveItemAsync(id, itemId);
            return NoContent();
        }

        [HttpPut(ApiRoutes.Invoices.Finalize)]
        public async Task<IActionResult> Finalize([FromRoute] Guid id)
        {
            InvoiceDto invoice = await _invoiceService.FinalizeAsync(id);
            return Ok(invoice);
        }

        [HttpPut(ApiRoutes.Invoices.Cancel)]
        public async Task<IActionResult> Cancel([FromRoute] Guid id)
        {
            InvoiceDto invoice = await _invoiceService.CancelAsync(id);
            return Ok(invoice);
        }

        // ════════════════════════════════════════════════════════════════════════════
        // SOFT DELETE OPERATIONS
        // ════════════════════════════════════════════════════════════════════════════
        [HttpDelete(ApiRoutes.Invoices.Delete)]
        public async Task<IActionResult> Delete([FromRoute] Guid id)
        {
            await _invoiceService.DeleteAsync(id);
            return NoContent();
        }

        [HttpPut(ApiRoutes.Invoices.Restore)]
        public async Task<IActionResult> Restore([FromRoute] Guid id)
        {
            await _invoiceService.RestoreAsync(id);
            return NoContent();
        }

        [HttpGet(ApiRoutes.Invoices.GetStats)]
        public async Task<ActionResult<InvoiceStatsDto>> GetStats([FromQuery] int top = 5)
            => Ok(await _invoiceService.GetStatsAsync(top));

        [HttpGet(ApiRoutes.Invoices.ToPdf)]
        public async Task<IActionResult> GetInvoicePdf([FromRoute] Guid id)
        {
            InvoiceDto invoice = await _invoiceService.GetByIdAsync(id);
            if (invoice == null)
                return NotFound();

            byte[] pdfBytes = await _pdfGenerator.GenerateInvoicePdf(invoice);
            return File(pdfBytes, "application/pdf", $"Invoice_{invoice.InvoiceNumber}.pdf");
        }


        // ════════════════════════════════════════════════════════════════════════════
        // ARTICLES CACHE
        // ════════════════════════════════════════════════════════════════════════════

        [HttpGet(ApiRoutes.Invoices.Cache.Articles.GetById)]
        public async Task<ActionResult<ArticleResponseDto?>> GetArticleCacheById([FromRoute] Guid id)
        {
            return Ok(await _articleCacheService.GetByIdAsync(id));
        }

        // invoices/cache/articles

        [HttpGet(ApiRoutes.Invoices.Cache.Articles.GetPaged)]
        public async Task<ActionResult<PagedResultDto<ArticleResponseDto>>> GetArticleCachePagedAsync([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10, [FromQuery] string? search = null)
        {
            return Ok(await _articleCacheService.GetPagedAsync(pageNumber, pageSize, search));
        }



        // ════════════════════════════════════════════════════════════════════════════
        // CLIENTS CACHE
        // ════════════════════════════════════════════════════════════════════════════
        [HttpGet(ApiRoutes.Invoices.Cache.Clients.GetById)]
        public async Task<ActionResult<ClientResponseDto?>> GetClientCacheById([FromRoute] Guid id)
        {
            return Ok(await _clientcacheService.GetByIdAsync(id));
        }

        // invoices/cache/clients

        [HttpGet(ApiRoutes.Invoices.Cache.Clients.GetPaged)]
        public async Task<ActionResult<PagedResultDto<ClientResponseDto>>> GetClientCachePagedAsync(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? search = null)
        {
            return Ok(await _clientcacheService.GetPagedAsync(pageNumber, pageSize, search));
        }


    }
}