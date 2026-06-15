using ERP.ArticleService.Application.DTOs;
using ERP.ArticleService.Application.Interfaces;
using ERP.ArticleService.Application.Services;
using ERP.ArticleService.Domain;
using ERP.ArticleService.Properties;
using Microsoft.AspNetCore.Mvc;

namespace ERP.ArticleService.API.Controllers
{
    [ApiController]
    public class CategoryController : ControllerBase
    {
        private readonly ICategoryService _categoryService;
        private readonly ITenantContext _tenantContext;

        public CategoryController(ICategoryService categoryService, ITenantContext tenantContext)
        {
            _tenantContext = tenantContext;
            _categoryService = categoryService;
        }

        // =========================
        // GET ALL
        // =========================
        [HttpGet(ApiRoutes.Categories.GetAll)]
        public async Task<ActionResult<List<Category>>> GetAll()
        {
            List<CategoryResponseDto> categories = await _categoryService.GetAllAsync();
            return Ok(categories);
        }

        [HttpGet(ApiRoutes.Categories.Stats)]
        public async Task<ActionResult<CategoryStatsDto>> GetStats()
        {
            CategoryStatsDto stats = await _categoryService.GetStatsAsync();
            return Ok(stats);
        }

        [HttpGet(ApiRoutes.Categories.GetDeleted)]
        public async Task<ActionResult> GetDeletedAsync(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10)
        {
            PagedResultDto<CategoryResponseDto> result = await _categoryService.GetPagedDeletedAsync(pageNumber, pageSize);
            return Ok(new { items = result.Items, totalCount = result.TotalCount });
        }


        // =========================
        // GET BY ID
        // =========================
        [HttpGet(ApiRoutes.Categories.GetById)]
        public async Task<ActionResult<Category>> GetById([FromRoute] Guid id)
        {
            CategoryResponseDto category = await _categoryService.GetByIdAsync(id);
            return Ok(category);
        }

        // =========================
        // GET BY NAME
        // =========================
        [HttpGet(ApiRoutes.Categories.GetByName)]
        public async Task<ActionResult<Category>> GetByName([FromQuery] string name)
        {
            CategoryResponseDto category = await _categoryService.GetByNameAsync(name);
            return Ok(category);
        }

        // =========================
        // GET PAGED
        // =========================
        [HttpGet(ApiRoutes.Categories.GetPaged)]
        public async Task<ActionResult> GetPaged(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10)
        {
            PagedResultDto<CategoryResponseDto> result = await _categoryService.GetPagedAsync(pageNumber, pageSize);
            return Ok(new { result.Items, result.TotalCount });
        }

        // =========================
        // TVA FILTERS
        // =========================
        [HttpGet(ApiRoutes.Categories.GetBelowTVA)]
        public async Task<ActionResult<List<Category>>> GetBelowTVA([FromQuery] decimal tva)
        {
            List<CategoryResponseDto> result = await _categoryService.GetBelowTVAAsync(tva);
            return Ok(result);
        }

        [HttpGet(ApiRoutes.Categories.GetHigherThanTVA)]
        public async Task<ActionResult<List<Category>>> GetHigherThanTVA([FromQuery] decimal tva)
        {
            List<CategoryResponseDto> result = await _categoryService.GetHigherThanTVAAsync(tva);
            return Ok(result);
        }

        [HttpGet(ApiRoutes.Categories.GetBetweenTVA)]
        public async Task<ActionResult<List<Category>>> GetBetweenTVA(
            [FromQuery] decimal min,
            [FromQuery] decimal max)
        {
            List<CategoryResponseDto> result = await _categoryService.GetBetweenTVAAsync(min, max);
            return Ok(result);
        }

        // =========================
        // GET PAGED BY DATE RANGE
        // =========================
        [HttpGet(ApiRoutes.Categories.GetByDateRange)]
        public async Task<ActionResult> GetPagedByDateRange(
            [FromQuery] DateTime from,
            [FromQuery] DateTime to,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10)
        {
            PagedResultDto<CategoryResponseDto> result = await _categoryService.GetPagedByDateRangeAsync(
                from, to, pageNumber, pageSize);

            return Ok(new { result.Items, result.TotalCount });
        }

        // =========================
        // CREATE
        // =========================
        [HttpPost(ApiRoutes.Categories.Create)]
        public async Task<ActionResult<Category>> Create([FromBody] CategoryRequestDto request)
        {
            if (_tenantContext.TenantId is null)
                return StatusCode(403, new { statusCode = 403, code = "TENANT_REQUIRED", message = "Tenant context is required." });


            CategoryResponseDto category = await _categoryService.CreateAsync(request);

            return CreatedAtAction(
                nameof(GetById),
                new { id = category.Id },
                category);
        }

        // =========================
        // UPDATE
        // =========================
        [HttpPut(ApiRoutes.Categories.Update)]
        public async Task<ActionResult<Category>> Update(
            [FromRoute] Guid id,
            [FromBody] CategoryRequestDto request)
        {
            CategoryResponseDto category = await _categoryService.UpdateAsync(id, request);
            return Ok(category);
        }

        // =========================
        // DELETE
        // =========================
        [HttpDelete(ApiRoutes.Categories.Delete)]
        public async Task<ActionResult> Delete([FromRoute] Guid id)
        {
            await _categoryService.DeleteAsync(id);
            return NoContent();
        }

        // =========================
        // RESTORE
        // =========================
        [HttpPatch(ApiRoutes.Categories.Restore)]
        public async Task<ActionResult> Restore([FromRoute] Guid id)
        {
            await _categoryService.RestoreAsync(id);
            return NoContent();
        }

    }
}