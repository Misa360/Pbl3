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

        private static CancellationTokenSource _articleListCts = new();

        public ArticleService(ApplicationDbContext db, IMemoryCache cache)
        {
            _db = db;
            _cache = cache;
        }

        // Invalidate every cached article list — called after create / approve / reject / toggle-featured / update / delete.
        private void InvalidateArticleListCaches()
        {
            // Cancel current token to signal all linked cache entries to expire
            var oldCts = Interlocked.Exchange(ref _articleListCts, new CancellationTokenSource());
            oldCts.Cancel();
            oldCts.Dispose();
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

        public async Task<Article?> GetArticleByIdUnfilteredAsync(int id)
        {
            return await _db.Articles.AsNoTracking()
                .Include(a => a.Category)
                .Include(a => a.Author)
                .Include(a => a.Comments!)
                    .ThenInclude(c => c.User)
                .AsSplitQuery()
                .FirstOrDefaultAsync(a => a.Id == id);
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

            _cache.Set(key, list, new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(LatestTtl)
                .AddExpirationToken(new Microsoft.Extensions.Primitives.CancellationChangeToken(_articleListCts.Token)));
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

            _cache.Set(key, featured, new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(FeaturedTtl)
                .AddExpirationToken(new Microsoft.Extensions.Primitives.CancellationChangeToken(_articleListCts.Token)));
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

            _cache.Set(key, list, new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(MostViewedTtl)
                .AddExpirationToken(new Microsoft.Extensions.Primitives.CancellationChangeToken(_articleListCts.Token)));
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
            // Tôn trọng Status mà caller đã set (Admin = 2/APPROVED, User = 1/PENDING).
            // Chỉ mặc định về PENDING khi caller không truyền giá trị nào.
            article.Status ??= 1;
            article.CreatedAt = DateTime.Now;
            article.UpdatedAt = DateTime.Now;

            // Ensure Category exists
            if (!await _db.Categories.AnyAsync(c => c.Id == article.CategoryId))
            {
                article.CategoryId = 1; 
            }

            article.Content = SanitizeHtml(article.Content);
            article.Slug = GenerateSlug(article.Title);
            if (article.Status == 2)
            {
                article.ModeratedAt = DateTime.Now;
                article.ModeratedBy = article.AuthorId;
            }
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
            article.Content = SanitizeHtml(updated.Content);
            article.ImageUrl = updated.ImageUrl ?? article.ImageUrl;
            
            // Ensure Category exists
            if (await _db.Categories.AnyAsync(c => c.Id == updated.CategoryId))
            {
                article.CategoryId = updated.CategoryId;
            }
            
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

        public async Task<bool> AdminUpdateArticleAsync(int id, string title, string? summary, string content,
            int categoryId, string? imageUrl, bool isFeatured)
        {
            var article = await _db.Articles.FirstOrDefaultAsync(a => a.Id == id && a.DeletedAt == null);
            if (article == null) return false;

            article.Title = title;
            article.Summary = summary;
            article.Content = SanitizeHtml(content);
            article.CategoryId = categoryId;
            article.IsFeatured = isFeatured;
            if (!string.IsNullOrEmpty(imageUrl)) article.ImageUrl = imageUrl;
            article.UpdatedAt = DateTime.Now;
            article.Slug = GenerateSlug(title);

            // Admin sửa thì auto-approve, kể cả bài đang pending hay bị reject.
            article.Status = 2;
            article.RejectReason = null;

            await _db.SaveChangesAsync();
            InvalidateArticleListCaches();
            return true;
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

            // Anti-spam: chỉ đếm 1 lượt xem cho cùng (user hoặc IP, bài) trong 30 phút
            var cutoff = DateTime.Now.AddMinutes(-30);
            bool recentlyViewed;
            if (userId.HasValue)
            {
                recentlyViewed = await _db.ArticleViews
                    .AnyAsync(v => v.ArticleId == id
                                && v.UserId == userId
                                && v.ViewedAt != null && v.ViewedAt >= cutoff);
            }
            else if (!string.IsNullOrEmpty(ipAddress))
            {
                recentlyViewed = await _db.ArticleViews
                    .AnyAsync(v => v.ArticleId == id
                                && v.UserId == null
                                && v.IpAddress == ipAddress
                                && v.ViewedAt != null && v.ViewedAt >= cutoff);
            }
            else
            {
                recentlyViewed = false;
            }

            if (recentlyViewed) return;

            // Atomic increment using raw SQL to prevent race conditions
            await _db.Database.ExecuteSqlRawAsync("UPDATE articles SET ViewCount = IFNULL(ViewCount, 0) + 1 WHERE Id = {0}", id);

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
        // SEARCH
        // ══════════════════════════════════════════════
        public async Task<List<Article>> SearchArticlesAsync(string keyword, int take = 30)
        {
            if (string.IsNullOrWhiteSpace(keyword)) return new List<Article>();
            var kw = keyword.Trim();
            var like = $"%{kw}%";

            return await _db.Articles.AsNoTracking()
                .Include(a => a.Category)
                .Include(a => a.Author)
                .Where(a => a.Status == 2 && a.DeletedAt == null
                            && (EF.Functions.Like(a.Title, like)
                                || EF.Functions.Like(a.Summary ?? "", like)
                                || EF.Functions.Like(a.Content ?? "", like)))
                .OrderByDescending(a => a.CreatedAt)
                .Take(take)
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
            await _db.SaveChangesAsync();
            InvalidateArticleListCaches();
            return true;
        }

        public async Task<bool> ToggleFocusArticleAsync(int articleId, int moderatorId)
        {
            var article = await _db.Articles.FindAsync(articleId);
            if (article == null) return false;

            article.IsFocus = !(article.IsFocus ?? false);
            await _db.SaveChangesAsync();
            InvalidateArticleListCaches();
            return true;
        }

        public async Task<bool> ToggleEventArticleAsync(int articleId, int moderatorId)
        {
            var article = await _db.Articles.FindAsync(articleId);
            if (article == null) return false;

            article.IsEvent = !(article.IsEvent ?? false);
            await _db.SaveChangesAsync();
            InvalidateArticleListCaches();
            return true;
        }

        public async Task<(List<Article> items, int total, int countAll, int countPublished, int countDraft, int countTrash)>
            GetAdminArticlesAsync(string status, string? q, int? categoryId, int page, int pageSize, string? view = null)
        {
            // Status counts (cùng filter q + categoryId).
            IQueryable<Article> baseQ = _db.Articles.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(q))
            {
                var like = $"%{q.Trim()}%";
                baseQ = baseQ.Where(a => EF.Functions.Like(a.Title, like) || EF.Functions.Like(a.Summary ?? "", like));
            }
            if (categoryId.HasValue && categoryId.Value > 0)
            {
                baseQ = baseQ.Where(a => a.CategoryId == categoryId.Value);
            }

            var countAll       = await baseQ.CountAsync(a => a.DeletedAt == null);
            var countPublished = await baseQ.CountAsync(a => a.DeletedAt == null && a.Status == 2);
            var countDraft     = await baseQ.CountAsync(a => a.DeletedAt == null && (a.Status == 1 || a.Status == 3));
            var countTrash     = await baseQ.CountAsync(a => a.DeletedAt != null);

            // Lọc theo tab.
            IQueryable<Article> q2 = baseQ.Include(a => a.Category).Include(a => a.Author);
            q2 = status switch
            {
                "published" => q2.Where(a => a.DeletedAt == null && a.Status == 2),
                "draft"     => q2.Where(a => a.DeletedAt == null && (a.Status == 1 || a.Status == 3)),
                "trash"     => q2.Where(a => a.DeletedAt != null),
                _           => q2.Where(a => a.DeletedAt == null),
            };

            if (view == "featured") q2 = q2.Where(a => a.IsFeatured == true);
            else if (view == "focus") q2 = q2.Where(a => a.IsFocus == true);
            else if (view == "comments") q2 = q2.Where(a => a.IsEvent == true);

            var total = await q2.CountAsync();

            var items = await q2
                .OrderByDescending(a => a.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, total, countAll, countPublished, countDraft, countTrash);
        }

        public async Task<int> BulkTrashAsync(int[] ids)
        {
            if (ids == null || ids.Length == 0) return 0;
            var now = DateTime.Now;
            var arts = await _db.Articles.Where(a => ids.Contains(a.Id) && a.DeletedAt == null).ToListAsync();
            foreach (var a in arts) a.DeletedAt = now;
            await _db.SaveChangesAsync();
            InvalidateArticleListCaches();
            return arts.Count;
        }

        public async Task<int> BulkRestoreAsync(int[] ids)
        {
            if (ids == null || ids.Length == 0) return 0;
            var arts = await _db.Articles.Where(a => ids.Contains(a.Id) && a.DeletedAt != null).ToListAsync();
            foreach (var a in arts) a.DeletedAt = null;
            await _db.SaveChangesAsync();
            InvalidateArticleListCaches();
            return arts.Count;
        }

        public async Task<int> BulkPermanentDeleteAsync(int[] ids)
        {
            if (ids == null || ids.Length == 0) return 0;
            var arts = await _db.Articles.Where(a => ids.Contains(a.Id)).ToListAsync();
            // Cascade dependent rows
            var artIds = arts.Select(a => a.Id).ToList();
            var views = _db.ArticleViews.Where(v => artIds.Contains(v.ArticleId));
            var comments = _db.ArticleComments.Where(c => artIds.Contains(c.ArticleId));
            var notifs = _db.Notifications.Where(n => n.ArticleId != null && artIds.Contains(n.ArticleId.Value));
            _db.ArticleViews.RemoveRange(views);
            _db.ArticleComments.RemoveRange(comments);
            _db.Notifications.RemoveRange(notifs);
            _db.Articles.RemoveRange(arts);
            await _db.SaveChangesAsync();
            InvalidateArticleListCaches();
            return arts.Count;
        }

        public async Task<Article?> DuplicateArticleAsync(int id, int currentUserId)
        {
            var src = await _db.Articles.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id);
            if (src == null) return null;

            var copy = new Article
            {
                Title = (src.Title ?? "(không tiêu đề)") + " (bản sao)",
                Slug = string.IsNullOrEmpty(src.Slug) ? null : (src.Slug + "-copy-" + DateTime.Now.Ticks.ToString().Substring(8)),
                Summary = src.Summary,
                Content = src.Content,
                ImageUrl = src.ImageUrl,
                VideoUrl = src.VideoUrl,
                AudioUrl = src.AudioUrl,
                CategoryId = src.CategoryId,
                AuthorId = currentUserId,
                Status = 1, // Bản nháp
                IsFeatured = false,
                ViewCount = 0,
                SubCategoryName = src.SubCategoryName,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now,
            };

            _db.Articles.Add(copy);
            await _db.SaveChangesAsync();
            InvalidateArticleListCaches();
            return copy;
        }

        public async Task<bool> QuickUpdateAsync(int id, string title, string? slug, int categoryId, int status, bool isFeatured, bool isFocus, bool isEvent)
        {
            var article = await _db.Articles.FirstOrDefaultAsync(a => a.Id == id);
            if (article == null) return false;

            if (!string.IsNullOrWhiteSpace(title)) article.Title = title.Trim();
            article.Slug = string.IsNullOrWhiteSpace(slug) ? null : slug.Trim();
            if (categoryId > 0) article.CategoryId = categoryId;
            if (status >= 1 && status <= 3) article.Status = status;
            article.IsFeatured = isFeatured;
            article.IsFocus = isFocus;
            article.IsEvent = isEvent;
            article.UpdatedAt = DateTime.Now;

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

        private string SanitizeHtml(string html)
        {
            if (string.IsNullOrWhiteSpace(html)) return string.Empty;

            // 1. Remove <script> blocks
            var sanitized = System.Text.RegularExpressions.Regex.Replace(html,
                @"<script\b[^>]*>([\s\S]*?)<\/script>", "",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            // 2. Remove dangerous tags like <iframe>, <object>, <embed>, <applet>
            sanitized = System.Text.RegularExpressions.Regex.Replace(sanitized,
                @"<(iframe|object|embed|applet|form|base|link|meta)\b[^>]*>([\s\S]*?)<\/\1>", "",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            sanitized = System.Text.RegularExpressions.Regex.Replace(sanitized,
                @"<(iframe|object|embed|applet|form|base|link|meta)\b[^>]*>", "",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            // 3. Remove event handlers (onmouseover, onclick, etc.)
            sanitized = System.Text.RegularExpressions.Regex.Replace(sanitized,
                @"\bon[a-z]+\s*=\s*""[^""]*""", "",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            sanitized = System.Text.RegularExpressions.Regex.Replace(sanitized,
                @"\bon[a-z]+\s*=\s*'[^']*'", "",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            // 4. Remove javascript: links
            sanitized = System.Text.RegularExpressions.Regex.Replace(sanitized,
                @"href\s*=\s*""javascript:[^""]*""", "href=\"#\"",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            return sanitized;
        }

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

            if (slug.Length > 200) {
                slug = slug.Substring(0, 200);
            }

            // Add timestamp for uniqueness
            slug += "-" + DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            return slug;
        }
    }
}