using ERP.ArticleService.Application.DTOs;
using ERP.ArticleService.Application.Interfaces;
using ERP.ArticleService.Application.Services;
using ERP.ArticleService.Infrastructure.Messaging;
using ERP.ArticleService.Properties;
using Microsoft.AspNetCore.Mvc;

namespace ERP.ArticleService.API.Controllers
{
    [ApiController]
    public class ArticleController : ControllerBase
    {
        private readonly IArticleService _articleService;
        private readonly ITenantContext _tenantContext;
        public ArticleController(IArticleService articleService, ITenantContext tenantContext)
        {
            _tenantContext = tenantContext;
            _articleService = articleService;
        }

        // =========================
        // GET ALL
        // =========================
        [HttpGet(ApiRoutes.Articles.GetAll)]
        public async Task<ActionResult<PagedResultDto<ArticleResponseDto>>> GetAllPagedAsync([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
        {
            PagedResultDto<ArticleResponseDto> result = await _articleService.GetAllAsync(pageNumber, pageSize);
            return Ok(new { result.Items, result.TotalCount });
        }


        // =========================
        // GET DELETED
        // =========================
        [HttpGet(ApiRoutes.Articles.GetDeletedRoute)]
        public async Task<ActionResult<PagedResultDto<ArticleResponseDto>>> GetDeletedAsync([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
        {
            PagedResultDto<ArticleResponseDto> result = await _articleService.GetPagedDeletedAsync(pageNumber, pageSize);
            return Ok(new { result.Items, result.TotalCount });
        }

        // =========================
        // GET BY ID
        // =========================
        [HttpGet(ApiRoutes.Articles.GetById)]
        public async Task<ActionResult<ArticleResponseDto>> GetById([FromRoute] Guid id)
        {
            ArticleResponseDto article = await _articleService.GetByIdAsync(id);
            return Ok(article);
        }

        // =========================
        // GET BY CODE
        // =========================
        [HttpGet(ApiRoutes.Articles.GetByCode)]
        public async Task<ActionResult<ArticleResponseDto>> GetByCode([FromQuery] string code)
        {
            ArticleResponseDto article = await _articleService.GetByCodeAsync(code);
            return Ok(article);
        }

        // =========================
        // GET PAGED BY CATEGORY
        // =========================
        [HttpGet(ApiRoutes.Articles.GetPagedByCategory)]
        public async Task<ActionResult<PagedResultDto<ArticleResponseDto>>> GetPagedByCategory([FromQuery] Guid categoryId, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
        {
            PagedResultDto<ArticleResponseDto> result = await _articleService.GetPagedByCategoryIdAsync(categoryId, pageNumber, pageSize);
            return Ok(new { result.Items, result.TotalCount });
        }

        [HttpGet(ApiRoutes.Articles.Stats)]
        [ProducesResponseType(typeof(ArticleStatsDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetStats()
        {
            ArticleStatsDto result = await _articleService.GetStatsAsync();
            return Ok(result);
        }


        // =========================
        // GET PAGED BY LIBELLE
        // =========================
        [HttpGet(ApiRoutes.Articles.GetPagedByLibelle)]
        public async Task<ActionResult<PagedResultDto<ArticleResponseDto>>> GetPagedByLibelle(
            [FromQuery] string libelleFilter,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10)
        {
            PagedResultDto<ArticleResponseDto> result = await _articleService.GetPagedByLibelleAsync(
                libelleFilter, pageNumber, pageSize);
            return Ok(new { result.Items, result.TotalCount });
        }

        // =========================
        // CREATE
        // =========================
        [HttpPost(ApiRoutes.Articles.Create)]
        public async Task<ActionResult<ArticleResponseDto>> Create([FromBody] CreateArticleRequestDto request)
        {
            if (_tenantContext.TenantId is null)
                return StatusCode(403, new { statusCode = 403, code = "TENANT_REQUIRED", message = "Tenant context is required." });

            ArticleResponseDto article = await _articleService.CreateAsync(request);
            return CreatedAtAction(
                nameof(GetById),
                new { id = article.Id },
                article);
        }

        // =========================
        // UPDATE
        // =========================
        [HttpPut(ApiRoutes.Articles.Update)]
        public async Task<ActionResult<ArticleResponseDto>> Update([FromRoute] Guid id, [FromBody] UpdateArticleRequestDto request)
        {
            ArticleResponseDto article = await _articleService.UpdateAsync(id, request);
            return Ok(article);
        }

        // =========================
        // RESTORE
        // =========================
        [HttpPatch(ApiRoutes.Articles.Restore)]
        public async Task<ActionResult<ArticleResponseDto>> Restore([FromRoute] Guid id)
        {
            await _articleService.RestoreAsync(id);
            return NoContent();
        }

        // =========================
        // DELETE
        // =========================
        [HttpDelete(ApiRoutes.Articles.Delete)]
        public async Task<ActionResult<ArticleResponseDto>> Delete([FromRoute] Guid id)
        {
            await _articleService.DeleteAsync(id);
            return NoContent();
        }
    }
}