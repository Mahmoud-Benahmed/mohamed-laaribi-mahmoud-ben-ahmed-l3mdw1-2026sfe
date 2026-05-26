using ERP.ArticleService.Application.DTOs;
using ERP.ArticleService.Application.Exceptions;
using ERP.ArticleService.Application.Interfaces;
using ERP.ArticleService.Domain;
using ERP.ArticleService.Infrastructure.Messaging;

namespace ERP.ArticleService.Application.Services
{
    public class ArticleService : IArticleService
    {
        private readonly IArticleRepository _articleRepository;
        private readonly ICategoryRepository _categoryRepository;
        private readonly IArticleCodeService _articleCodeService;
        private readonly IEventPublisher _eventPublisher;
        private readonly ILogger<ArticleService> logger;
        private readonly ITenantContext _tenantContext;

        public ArticleService(
            IArticleRepository articleRepository,
            ICategoryRepository categoryRepository,
            IArticleCodeService articleCodeService,
            IEventPublisher eventPublisher,
            ILogger<ArticleService> _logger,
            ITenantContext tenantContext)
        {
            _articleRepository = articleRepository;
            _categoryRepository = categoryRepository;
            _articleCodeService = articleCodeService;
            _eventPublisher = eventPublisher;
            logger = _logger;
            _tenantContext = tenantContext;
        }

        // =========================
        // CREATE
        // =========================
        public async Task<ArticleResponseDto> CreateAsync(CreateArticleRequestDto request)
        {
            Category category = await _categoryRepository.GetByIdAsync(request.CategoryId)
                ?? throw new KeyNotFoundException(
                    $"Category with id '{request.CategoryId}' was not found.");

            Article? existing = await _articleRepository.GetByBarCodeAsync(request.BarCode);
            if (existing is not null)
                throw new ArticleAlreadyExistsException(existing.BarCode);

            string code = await _articleCodeService.GenerateArticleCodeAsync();

            Article article = new Article(code, request.Libelle, request.Prix, request.Unit, category, request.BarCode, request.TVA, _tenantContext.TenantId);
            await _articleRepository.AddAsync(article);
            await _articleRepository.SaveChangesAsync();
            ArticleResponseDto dto = MapToDto(article);

            await _eventPublisher.PublishAsync(ArticleTopics.Created, dto);
            return dto;
        }

        // =========================
        // READ
        // =========================
        public async Task<ArticleResponseDto> GetByIdAsync(Guid id)
        {
            Article? article = await _articleRepository.GetByIdAsync(id);
            if (article is null || article.IsDeleted)
                throw new ArticleNotFoundException(id);
            return MapToDto(article);
        }

        public async Task<ArticleResponseDto> GetByCodeAsync(string code)
        {
            Article? article = await _articleRepository.GetByCodeAsync(code);
            if (article is null || article.IsDeleted)
                throw new ArticleNotFoundException(code);
            return MapToDto(article);
        }

        // =========================
        // UPDATE
        // =========================
        public async Task<ArticleResponseDto> UpdateAsync(Guid id, UpdateArticleRequestDto request)
        {
            Article? article = await _articleRepository.GetByIdAsync(id) ?? throw new ArticleNotFoundException(id);

            if (article is null || article.IsDeleted)
                throw new ArticleNotFoundException(id);

            Category category = await _categoryRepository.GetByIdAsync(request.CategoryId)
                ?? throw new CategoryNotFoundException(request.CategoryId);

            article.Update(request.Libelle, request.Prix, request.Unit, category, request.BarCode, request.TVA);

            await _articleRepository.SaveChangesAsync();
            ArticleResponseDto dto = MapToDto(article);
            await _eventPublisher.PublishAsync(ArticleTopics.Updated, dto);

            return dto;
        }

        // =========================
        // RESTORE
        // =========================
        public async Task RestoreAsync(Guid id)
        {
            Article article = await _articleRepository.GetByIdDeletedAsync(id) ?? throw new ArticleNotFoundException(id);
            if (!article.IsDeleted)
                return;

            article.Restore();
            await _articleRepository.SaveChangesAsync();

            ArticleResponseDto dto = MapToDto(article);
            await _eventPublisher.PublishAsync(ArticleTopics.Restored, dto);

        }


        // =========================
        // RESTORE
        // =========================
        public async Task DeleteAsync(Guid id)
        {
            Article article = await _articleRepository.GetByIdDeletedAsync(id) ?? throw new ArticleNotFoundException(id);
            if (article.IsDeleted)
                return;

            article.Delete();
            await _articleRepository.SaveChangesAsync();

            ArticleResponseDto dto = MapToDto(article);
            await _eventPublisher.PublishAsync(ArticleTopics.Deleted, dto);

        }

        // =========================
        // PAGING / FILTERING
        // =========================
        public async Task<PagedResultDto<ArticleResponseDto>> GetAllAsync(int pageNumber, int pageSize)
        {
            ValidatePaging(pageNumber, pageSize);

            (List<Article>? items, int totalCount) = await _articleRepository.GetAllAsync(pageNumber, pageSize);
            List<ArticleResponseDto> mappedItems = items.Select(MapToDto).ToList();

            return new PagedResultDto<ArticleResponseDto>(mappedItems, totalCount, pageNumber, pageSize);
        }

        public async Task<PagedResultDto<ArticleResponseDto>> GetPagedByCategoryIdAsync(Guid categoryId, int pageNumber, int pageSize)
        {
            ValidatePaging(pageNumber, pageSize);
            (List<Article>? items, int totalCount) = await _articleRepository
                .GetPagedByCategoryIdAsync(categoryId, pageNumber, pageSize);
            List<ArticleResponseDto> mappedItems = items.Select(MapToDto).ToList();

            return new PagedResultDto<ArticleResponseDto>(mappedItems, totalCount, pageNumber, pageSize);
        }

        public async Task<PagedResultDto<ArticleResponseDto>> GetPagedDeletedAsync(int pageNumber, int pageSize)
        {
            ValidatePaging(pageNumber, pageSize);
            (List<Article>? items, int totalCount) = await _articleRepository
                .GetPagedDeletedAsync(pageNumber, pageSize);
            List<ArticleResponseDto> mappedItems = items.Select(MapToDto).ToList();

            return new PagedResultDto<ArticleResponseDto>(mappedItems, totalCount, pageNumber, pageSize);
        }

        public async Task<PagedResultDto<ArticleResponseDto>> GetPagedByLibelleAsync(string libelleFilter, int pageNumber, int pageSize)
        {
            ValidatePaging(pageNumber, pageSize);
            if (string.IsNullOrWhiteSpace(libelleFilter))
                throw new ArgumentException("Libelle filter cannot be empty.");

            (List<Article>? items, int totalCount) = await _articleRepository
                .GetPagedByLibelleAsync(libelleFilter, pageNumber, pageSize);
            List<ArticleResponseDto> mappedItems = items.Select(MapToDto).ToList();

            return new PagedResultDto<ArticleResponseDto>(mappedItems, totalCount, pageNumber, pageSize);
        }


        // ======================
        // STATS
        // ======================
        public async Task<ArticleStatsDto> GetStatsAsync()
        {
            return await _articleRepository.GetStatsAsync();
        }

        // =========================
        // PRIVATE HELPERS
        // =========================
        private static void ValidatePaging(int pageNumber, int pageSize)
        {
            if (pageNumber < 1)
                throw new ArgumentOutOfRangeException(nameof(pageNumber),
                    "Page number must be greater than zero.");
            if (pageSize < 1)
                throw new ArgumentOutOfRangeException(nameof(pageSize),
                    "Page size must be greater than zero.");
        }

        private static CategoryResponseDto CategoryMapToDto(Category cat) => new CategoryResponseDto(
            Id: cat.Id,
            Name: cat.Name,
            TVA: cat.TVA,
            IsDeleted: cat.IsDeleted,
            CreatedAt: cat.CreatedAt,
            UpdatedAt: cat.UpdatedAt,
            TenantId: cat.TenantId
            );
        private static ArticleResponseDto MapToDto(Article article) => new ArticleResponseDto(
            Id: article.Id,
            Category: CategoryMapToDto(article.Category),
            CodeRef: article.CodeRef,
            BarCode: article.BarCode,
            Libelle: article.Libelle,
            Prix: article.Prix,
            Unit: article.Unit.ToString(),
            TVA: article.TVA,
            IsDeleted: article.IsDeleted,
            CreatedAt: article.CreatedAt,
            UpdatedAt: article.UpdatedAt,
            TenantId: article.TenantId
            );
    }
}