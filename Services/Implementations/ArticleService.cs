using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using DaNangSafeMap.Data;
using DaNangSafeMap.Models.Entities;
using DaNangSafeMap.Services.Interfaces;

namespace DaNangSafeMap.Services.Implementations
{
    public class ArticleService : IArticleService
    {
        private readonly ApplicationDbContext _db;
        private readonly IMemoryCache _cache;

        // Cache TTLs — short enough that moderated content appears quickly,
        // long enough that a popular news page stops hammering MySQL.
        private static readonly TimeSpan LatestTtl       = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan FeaturedTtl     = TimeSpan.FromSeconds(60);
        private static readonly TimeSpan MostViewedTtl   = TimeSpan.FromSeconds(60);
        private static readonly TimeSpan CategoriesTtl   = TimeSpan.FromMinutes(10);

        public ArticleService(ApplicationDbContext db, IMemoryCache cache)
        {
            _db = db;
            _cache = cache;
        }

        // Invalidate every cached article list — called after create / approve / reject / toggle-featured / update / delete.
        private void InvalidateArticleListCaches()
        {
            // Simple broad invalidation: remove all known cache keys.
            // Keys are short and enumerable, so we can just drop them.
            _cache.Remove("art:latest:-:10");
            _cache.Remove("art:latest:-:12");
            _cache.Remove("art:latest:an-ninh:20");
            _cache.Remove("art:latest:an-ninh:24");
            _cache.Remove("art:latest:an-ninh:7");
            _cache.Remove("art:latest:doi-song:20");
            _cache.Remove("art:latest:doi-song:24");
            _cache.Remove("art:latest:doi-song:7");
            _cache.Remove("art:featured:-:4");
            _cache.Remove("art:featured:an-ninh:4");
            _cache.Remove("art:featured:doi-song:4");
            _cache.Remove("art:mostviewed:10");
            _cache.Remove("art:categories");
        }

        // ══════════════════════════════════════════════
        // ARTICLES
        // ══════════════════════════════════════════════

        public async Task<List<Article>> GetArticlesByCategoryAsync(string slug, int page = 1, int pageSize = 20)
        {
            var query = _db.Articles.AsNoTracking()
                .Include(a => a.Category)
                .Include(a => a.Author)
                .Where(a => a.Status == 2 && a.DeletedAt == null);

            if (!string.IsNullOrEmpty(slug) && slug != "all")
            {
                query = query.Where(a => a.Category != null && a.Category.Slug == slug);
            }

            return await query
                .OrderByDescending(a => a.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(a => new Article
                {
                    Id = a.Id,
                    Title = a.Title,
                    Slug = a.Slug,
                    Summary = a.Summary,
                    ImageUrl = a.ImageUrl,
                    CategoryId = a.CategoryId,
                    Category = a.Category,
                    AuthorId = a.AuthorId,
                    Author = a.Author,
                    CreatedAt = a.CreatedAt,
                    ViewCount = a.ViewCount,
                    Status = a.Status
                })
                .ToListAsync();
        }

        public async Task<int> GetArticleCountByCategoryAsync(string slug)
        {
            var query = _db.Articles.AsNoTracking().Where(a => a.Status == 2 && a.DeletedAt == null);
            if (!string.IsNullOrEmpty(slug) && slug != "all")
            {
                query = query.Where(a => a.Category != null && a.Category.Slug == slug);
            }
            return await query.CountAsync();
        }

        public async Task<Article?> GetArticleByIdAsync(int id)
        {
            return await _db.Articles.AsNoTracking()
                .Include(a => a.Category)
                .Include(a => a.Author)
                .Include(a => a.Comments!)
                    .ThenInclude(c => c.User)
                .AsSplitQuery()
                .FirstOrDefaultAsync(a => a.Id == id && a.DeletedAt == null);
        }

        public async Task<List<Article>> GetLatestArticlesAsync(int count = 10, string? categorySlug = null)
        {
            var key = $"art:latest:{(string.IsNullOrEmpty(categorySlug) ? "-" : categorySlug)}:{count}";
            if (_cache.TryGetValue(key, out List<Article>? cached) && cached != null)
            {
                return cached;
            }

            var query = _db.Articles.AsNoTracking()
                .Where(a => a.Status == 2 && a.DeletedAt == null);

            if (!string.IsNullOrEmpty(categorySlug))
            {
                query = query.Where(a => a.Category != null && a.Category.Slug == categorySlug);
            }

            var list = await query
                .OrderByDescending(a => a.CreatedAt)
                .Take(count)
                .Select(a => new Article
                {
                    Id = a.Id,
                    Title = a.Title,
                    Slug = a.Slug,
                    Summary = a.Summary,
                    ImageUrl = a.ImageUrl,
                    CategoryId = a.CategoryId,
                    Category = a.Category,
                    AuthorId = a.AuthorId,
                    Author = a.Author,
                    CreatedAt = a.CreatedAt,
                    ViewCount = a.ViewCount,
                    Status = a.Status
                })
                .ToListAsync();

            _cache.Set(key, list, LatestTtl);
            return list;
        }

        public async Task<List<Article>> GetFeaturedArticlesAsync(int count = 4, string? categorySlug = null)
        {
            var key = $"art:featured:{(string.IsNullOrEmpty(categorySlug) ? "-" : categorySlug)}:{count}";
            if (_cache.TryGetValue(key, out List<Article>? cached) && cached != null)
            {
                return cached;
            }

            var query = _db.Articles.AsNoTracking()
                .Where(a => a.Status == 2 && a.DeletedAt == null && a.IsFeatured == true);

            if (!string.IsNullOrEmpty(categorySlug))
            {
                query = query.Where(a => a.Category != null && a.Category.Slug == categorySlug);
            }

            var featured = await query
                .OrderByDescending(a => a.CreatedAt)
                .Take(count)
                .Select(a => new Article
                {
                    Id = a.Id,
                    Title = a.Title,
                    Slug = a.Slug,
                    Summary = a.Summary,
                    ImageUrl = a.ImageUrl,
                    CategoryId = a.CategoryId,
                    Category = a.Category,
                    AuthorId = a.AuthorId,
                    Author = a.Author,
                    CreatedAt = a.CreatedAt,
                    ViewCount = a.ViewCount,
                    Status = a.Status,
                    IsFeatured = a.IsFeatured
                })
                .ToListAsync();

            // Nếu không đủ featured, lấy thêm bài mới nhất
            if (featured.Count < count)
            {
                var existingIds = featured.Select(f => f.Id).ToList();
                var more = await _db.Articles.AsNoTracking()
                    .Where(a => a.Status == 2 && a.DeletedAt == null && !existingIds.Contains(a.Id))
                    .Where(a => string.IsNullOrEmpty(categorySlug) || (a.Category != null && a.Category.Slug == categorySlug))
                    .OrderByDescending(a => a.CreatedAt)
                    .Take(count - featured.Count)
                    .Select(a => new Article
                    {
                        Id = a.Id,
                        Title = a.Title,
                        Slug = a.Slug,
                        Summary = a.Summary,
                        ImageUrl = a.ImageUrl,
                        CategoryId = a.CategoryId,
                        Category = a.Category,
                        AuthorId = a.AuthorId,
                        Author = a.Author,
                        CreatedAt = a.CreatedAt,
                        ViewCount = a.ViewCount,
                        Status = a.Status,
                        IsFeatured = a.IsFeatured
                    })
                    .ToListAsync();
                featured.AddRange(more);
            }

            _cache.Set(key, featured, FeaturedTtl);
            return featured;
        }

        public async Task<List<Article>> GetMostViewedArticlesAsync(int count = 10)
        {
            var key = $"art:mostviewed:{count}";
            if (_cache.TryGetValue(key, out List<Article>? cached) && cached != null)
            {
                return cached;
            }

            var list = await _db.Articles.AsNoTracking()
                .Where(a => a.Status == 2 && a.DeletedAt == null)
                .OrderByDescending(a => a.ViewCount)
                .Take(count)
                .Select(a => new Article
                {
                    Id = a.Id,
                    Title = a.Title,
                    Slug = a.Slug,
                    Summary = a.Summary,
                    ImageUrl = a.ImageUrl,
                    CategoryId = a.CategoryId,
                    Category = a.Category,
                    AuthorId = a.AuthorId,
                    Author = a.Author,
                    CreatedAt = a.CreatedAt,
                    ViewCount = a.ViewCount,
                    Status = a.Status
                })
                .ToListAsync();

            _cache.Set(key, list, MostViewedTtl);
            return list;
        }

        public async Task<List<Article>> GetRelatedArticlesAsync(int articleId, int count = 5)
        {
            var article = await _db.Articles.FindAsync(articleId);
            if (article == null) return new List<Article>();

            return await _db.Articles.AsNoTracking()
                .Include(a => a.Category)
                .Include(a => a.Author)
                .Where(a => a.Id != articleId
                    && a.Status == 2
                    && a.DeletedAt == null
                    && a.CategoryId == article.CategoryId)
                .OrderByDescending(a => a.CreatedAt)
                .Take(count)
                .Select(a => new Article
                {
                    Id = a.Id,
                    Title = a.Title,
                    Slug = a.Slug,
                    Summary = a.Summary,
                    ImageUrl = a.ImageUrl,
                    CategoryId = a.CategoryId,
                    Category = a.Category,
                    AuthorId = a.AuthorId,
                    Author = a.Author,
                    CreatedAt = a.CreatedAt,
                    ViewCount = a.ViewCount,
                    Status = a.Status
                })
                .ToListAsync();
        }

        public async Task<Article> CreateArticleAsync(Article article)
        {
            article.Status = 1; // PENDING
            article.CreatedAt = DateTime.Now;
            article.UpdatedAt = DateTime.Now;
            article.Slug = GenerateSlug(article.Title);
            _db.Articles.Add(article);
            await _db.SaveChangesAsync();
            InvalidateArticleListCaches();
            return article;
        }

        public async Task<Article?> UpdateArticleAsync(int id, int userId, Article updated)
        {
            var article = await _db.Articles.FirstOrDefaultAsync(a => a.Id == id && a.AuthorId == userId && a.DeletedAt == null);
            if (article == null) return null;

            article.Title = updated.Title;
            article.Summary = updated.Summary;
            article.Content = updated.Content;
            article.ImageUrl = updated.ImageUrl ?? article.ImageUrl;
            article.CategoryId = updated.CategoryId;
            article.SubCategoryName = updated.SubCategoryName;
            article.UpdatedAt = DateTime.Now;
            article.Slug = GenerateSlug(updated.Title);

            // Nếu đang bị reject, chuyển lại pending khi sửa
            if (article.Status == 3)
            {
                article.Status = 1;
                article.RejectReason = null;
            }

            await _db.SaveChangesAsync();
            InvalidateArticleListCaches();
            return article;
        }

        public async Task<bool> DeleteArticleAsync(int id, int userId)
        {
            var article = await _db.Articles.FirstOrDefaultAsync(a => a.Id == id && a.AuthorId == userId);
            if (article == null) return false;
            article.DeletedAt = DateTime.Now;
            await _db.SaveChangesAsync();
            InvalidateArticleListCaches();
            return true;
        }

        public async Task IncrementViewCountAsync(int id, string? ipAddress, int? userId)
        {
            var article = await _db.Articles.FindAsync(id);
            if (article == null) return;

            article.ViewCount = (article.ViewCount ?? 0) + 1;

            _db.ArticleViews.Add(new ArticleView
            {
                ArticleId = id,
                UserId = userId,
                IpAddress = ipAddress,
                ViewedAt = DateTime.Now
            });

            await _db.SaveChangesAsync();
        }

        // ══════════════════════════════════════════════
        // USER'S ARTICLES
        // ══════════════════════════════════════════════

        public async Task<List<Article>> GetUserArticlesAsync(int userId)
        {
            return await _db.Articles.AsNoTracking()
                .Include(a => a.Category)
                .Where(a => a.AuthorId == userId && a.DeletedAt == null)
                .OrderByDescending(a => a.CreatedAt)
                .ToListAsync();
        }

        // ══════════════════════════════════════════════
        // COMMENTS
        // ══════════════════════════════════════════════

        public async Task<ArticleComment> AddCommentAsync(int articleId, int userId, string content)
        {
            var comment = new ArticleComment
            {
                ArticleId = articleId,
                UserId = userId,
                Content = content,
                CreatedAt = DateTime.Now
            };
            _db.ArticleComments.Add(comment);
            await _db.SaveChangesAsync();

            // Load user info
            await _db.Entry(comment).Reference(c => c.User).LoadAsync();
            return comment;
        }

        public async Task<List<ArticleComment>> GetCommentsAsync(int articleId)
        {
            return await _db.ArticleComments.AsNoTracking()
                .Include(c => c.User)
                .Where(c => c.ArticleId == articleId)
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();
        }

        // ══════════════════════════════════════════════
        // NOTIFICATIONS
        // ══════════════════════════════════════════════

        public async Task<List<Notification>> GetUserNotificationsAsync(int userId)
        {
            return await _db.Notifications.AsNoTracking()
                .Where(n => n.UserId == userId)
                .OrderByDescending(n => n.CreatedAt)
                .Take(50)
                .ToListAsync();
        }

        public async Task<int> GetUnreadNotificationCountAsync(int userId)
        {
            return await _db.Notifications
                .CountAsync(n => n.UserId == userId && n.IsRead == false);
        }

        public async Task MarkNotificationReadAsync(int id, int userId)
        {
            var n = await _db.Notifications.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
            if (n != null) { n.IsRead = true; await _db.SaveChangesAsync(); }
        }

        public async Task MarkAllNotificationsReadAsync(int userId)
        {
            var unread = await _db.Notifications.Where(n => n.UserId == userId && n.IsRead == false).ToListAsync();
            foreach (var n in unread) n.IsRead = true;
            await _db.SaveChangesAsync();
        }

        // ══════════════════════════════════════════════
        // ADMIN
        // ══════════════════════════════════════════════

        public async Task<List<Article>> GetPendingArticlesAsync()
        {
            return await _db.Articles.AsNoTracking()
                .Include(a => a.Category)
                .Include(a => a.Author)
                .Where(a => a.Status == 1 && a.DeletedAt == null)
                .OrderByDescending(a => a.CreatedAt)
                .Select(a => new Article
                {
                    Id = a.Id,
                    Title = a.Title,
                    Slug = a.Slug,
                    Summary = a.Summary,
                    ImageUrl = a.ImageUrl,
                    CategoryId = a.CategoryId,
                    Category = a.Category,
                    AuthorId = a.AuthorId,
                    Author = a.Author,
                    CreatedAt = a.CreatedAt,
                    Status = a.Status
                })
                .ToListAsync();
        }

        public async Task<List<Article>> GetAllApprovedArticlesAsync(int page = 1, int pageSize = 30)
        {
            return await _db.Articles.AsNoTracking()
                .Include(a => a.Category)
                .Include(a => a.Author)
                .Where(a => a.Status == 2 && a.DeletedAt == null)
                .OrderByDescending(a => a.IsFeatured)
                .ThenByDescending(a => a.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(a => new Article
                {
                    Id = a.Id,
                    Title = a.Title,
                    Slug = a.Slug,
                    Summary = a.Summary,
                    ImageUrl = a.ImageUrl,
                    CategoryId = a.CategoryId,
                    Category = a.Category,
                    AuthorId = a.AuthorId,
                    Author = a.Author,
                    CreatedAt = a.CreatedAt,
                    Status = a.Status,
                    IsFeatured = a.IsFeatured
                })
                .ToListAsync();
        }

        public async Task<bool> ApproveArticleAsync(int articleId, int moderatorId)
        {
            var article = await _db.Articles.Include(a => a.Author).FirstOrDefaultAsync(a => a.Id == articleId);
            if (article == null) return false;

            article.Status = 2; // APPROVED
            article.ModeratedBy = moderatorId;
            article.ModeratedAt = DateTime.Now;

            // Gửi thông báo cho tác giả
            _db.Notifications.Add(new Notification
            {
                UserId = article.AuthorId,
                ArticleId = articleId,
                Title = "Bài viết đã được duyệt ✅",
                Content = $"Bài viết \"{article.Title}\" của bạn đã được duyệt và đăng trên trang tin tức.",
                Type = "APPROVED",
                IsRead = false,
                CreatedAt = DateTime.Now
            });

            await _db.SaveChangesAsync();
            InvalidateArticleListCaches();
            return true;
        }

        public async Task<bool> RejectArticleAsync(int articleId, int moderatorId, string reason)
        {
            var article = await _db.Articles.Include(a => a.Author).FirstOrDefaultAsync(a => a.Id == articleId);
            if (article == null) return false;

            article.Status = 3; // REJECTED
            article.ModeratedBy = moderatorId;
            article.ModeratedAt = DateTime.Now;
            article.RejectReason = reason;

            // Gửi thông báo cho tác giả
            _db.Notifications.Add(new Notification
            {
                UserId = article.AuthorId,
                ArticleId = articleId,
                Title = "Bài viết bị từ chối ❌",
                Content = $"Bài viết \"{article.Title}\" bị từ chối. Lý do: {reason}",
                Type = "REJECTED",
                IsRead = false,
                CreatedAt = DateTime.Now
            });

            await _db.SaveChangesAsync();
            InvalidateArticleListCaches();
            return true;
        }

        public async Task<bool> ToggleFeaturedArticleAsync(int articleId, int moderatorId)
        {
            var article = await _db.Articles.FindAsync(articleId);
            if (article == null) return false;

            article.IsFeatured = !(article.IsFeatured ?? false);
            article.ModeratedBy = moderatorId;
            
            await _db.SaveChangesAsync();
            InvalidateArticleListCaches();
            return true;
        }

        // ══════════════════════════════════════════════
        // CATEGORIES
        // ══════════════════════════════════════════════

        public async Task<List<Category>> GetCategoriesAsync()
        {
            const string key = "art:categories";
            if (_cache.TryGetValue(key, out List<Category>? cached) && cached != null)
            {
                return cached;
            }
            var list = await _db.Categories.AsNoTracking().OrderBy(c => c.Id).ToListAsync();
            _cache.Set(key, list, CategoriesTtl);
            return list;
        }

        public async Task<Category?> GetCategoryBySlugAsync(string slug)
        {
            return await _db.Categories.AsNoTracking().FirstOrDefaultAsync(c => c.Slug == slug);
        }

        // ══════════════════════════════════════════════
        // HELPERS
        // ══════════════════════════════════════════════

        private string GenerateSlug(string title)
        {
            var slug = title.ToLower().Trim();
            // Vietnamese diacritics removal
            var chars = "àáạảãâầấậẩẫăằắặẳẵèéẹẻẽêềếệểễìíịỉĩòóọỏõôồốộổỗơờớợởỡùúụủũưừứựửữỳýỵỷỹđ";
            var norms = "aaaaaaaaaaaaaaaaaeeeeeeeeeeeiiiiiooooooooooooooooouuuuuuuuuuuyyyyyd";
            for (int i = 0; i < chars.Length; i++)
                slug = slug.Replace(chars[i], norms[i]);

            slug = System.Text.RegularExpressions.Regex.Replace(slug, @"[^a-z0-9\s-]", "");
            slug = System.Text.RegularExpressions.Regex.Replace(slug, @"\s+", "-");
            slug = System.Text.RegularExpressions.Regex.Replace(slug, @"-+", "-");
            slug = slug.Trim('-');

            // Add timestamp for uniqueness
            slug += "-" + DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            return slug;
        }
    }
}
