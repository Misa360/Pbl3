using DaNangSafeMap.Models.Entities;

namespace DaNangSafeMap.Services.Interfaces
{
    public interface IArticleService
    {
        Task<IEnumerable<Article>> GetAllArticlesAsync();
        Task<Article?> GetArticleByIdAsync(int id);
        Task<bool> CreateArticleAsync(Article article);
        Task<bool> UpdateArticleAsync(Article article);
        Task<bool> DeleteArticleAsync(int id);
        Task<List<Article>> GetRelatedArticlesAsync(int categoryId, int currentArticleId, int count);
    }
}
