using ERP.ArticleService.Domain;

namespace ERP.ArticleService.Application.Interfaces
{
    public interface IArticleCodeRepository
    {
        Task<ArticleCode> AddAsync(ArticleCode code);

        /// <summary>
        /// Generates a new unique article code using the single row in the ArticleCode table.
        /// Must be atomic to avoid duplicate codes.
        /// </summary>
        Task<string> GenerateArticleCodeAsync();
    }
}