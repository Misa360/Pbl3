using DaNangSafeMap.Models.Entities;

namespace DaNangSafeMap.Services.Interfaces
{
    public interface IArticleService
    {
        // ── Articles ──
        Task<List<Article>> GetArticlesByCategoryAsync(string slug, int page = 1, int pageSize = 20);
        Task<int> GetArticleCountByCategoryAsync(string slug);
        Task<Article?> GetArticleByIdAsync(int id);
        Task<List<Article>> GetLatestArticlesAsync(int count = 10, string? categorySlug = null);
        Task<List<Article>> GetFeaturedArticlesAsync(int count = 4, string? categorySlug = null);
        Task<List<Article>> GetMostViewedArticlesAsync(int count = 10);
        Task<List<Article>> GetRelatedArticlesAsync(int articleId, int count = 5);
        Task<Article> CreateArticleAsync(Article article);
        Task<Article?> UpdateArticleAsync(int id, int userId, Article updated);
        Task<bool> DeleteArticleAsync(int id, int userId);
        Task IncrementViewCountAsync(int id, string? ipAddress, int? userId);

        // ── Search ──
        Task<List<Article>> SearchArticlesAsync(string keyword, int take = 30);

        // ── User's articles ──
        Task<List<Article>> GetUserArticlesAsync(int userId);

        // ── Comments ──
        Task<ArticleComment> AddCommentAsync(int articleId, int userId, string content);
        Task<List<ArticleComment>> GetCommentsAsync(int articleId);

        // ── Notifications ──
        Task<List<Notification>> GetUserNotificationsAsync(int userId);
        Task<int> GetUnreadNotificationCountAsync(int userId);
        Task MarkNotificationReadAsync(int id, int userId);
        Task MarkAllNotificationsReadAsync(int userId);

        // ── Admin ──
        Task<List<Article>> GetPendingArticlesAsync();
        Task<List<Article>> GetAllApprovedArticlesAsync(int page = 1, int pageSize = 30);
        Task<bool> ApproveArticleAsync(int articleId, int moderatorId);
        Task<bool> RejectArticleAsync(int articleId, int moderatorId, string reason);
        Task<bool> ToggleFeaturedArticleAsync(int articleId, int moderatorId);

        // ── Categories ──
        Task<List<Category>> GetCategoriesAsync();
        Task<Category?> GetCategoryBySlugAsync(string slug);
    }
}
