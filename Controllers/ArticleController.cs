using Microsoft.AspNetCore.Mvc;
using DaNangSafeMap.Services.Interfaces;
using DaNangSafeMap.Models.Entities;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;

namespace DaNangSafeMap.Controllers
{
    public class ArticleController : Controller
    {
        private readonly IArticleService _articleService;
        private readonly IWebHostEnvironment _env;

        public ArticleController(IArticleService articleService, IWebHostEnvironment env)
        {
            _articleService = articleService;
            _env = env;
        }

        // ══════════════════════════════════════════════
        // HELPER: Lấy UserId từ JWT cookie
        // ══════════════════════════════════════════════
        private int? GetCurrentUserId()
        {
            var token = Request.Cookies["jwtToken"];
            if (string.IsNullOrEmpty(token)) return null;
            try
            {
                var handler = new JwtSecurityTokenHandler();
                var jwt = handler.ReadJwtToken(token);
                var idClaim = jwt.Claims.FirstOrDefault(c => c.Type == "nameid" || c.Type == ClaimTypes.NameIdentifier);
                return idClaim != null ? int.Parse(idClaim.Value) : null;
            }
            catch { return null; }
        }

        private string? GetCurrentUserRole()
        {
            var token = Request.Cookies["jwtToken"];
            if (string.IsNullOrEmpty(token)) return null;
            try
            {
                var handler = new JwtSecurityTokenHandler();
                var jwt = handler.ReadJwtToken(token);
                var roleClaim = jwt.Claims.FirstOrDefault(c => c.Type == "role" || c.Type == ClaimTypes.Role);
                return roleClaim?.Value;
            }
            catch { return null; }
        }

        // ══════════════════════════════════════════════
        // DANH SÁCH BÀI VIẾT TỔNG HỢP (Home, Category, Admin, Personal)
        // ══════════════════════════════════════════════
        [HttpGet]
        public async Task<IActionResult> Index(string? categorySlug = null, string? mode = null, int page = 1)
        {
            var userId = GetCurrentUserId();
            var role = GetCurrentUserRole();

            ViewBag.Mode = mode ?? "public";
            ViewBag.CurrentCategory = categorySlug;
            ViewBag.CurrentPage = page;
            ViewBag.UserId = userId;
            ViewBag.Role = role;

            if (mode == "admin")
            {
                if (role != "Admin") return Forbid();
                var pending = await _articleService.GetPendingArticlesAsync();
                var allApproved = await _articleService.GetAllApprovedArticlesAsync(1, 50);
                ViewBag.AllApproved = allApproved;
                return View("Index", pending);
            }
            if (mode == "my")
            {
                if (userId == null) return Redirect("/Auth/Login");
                var myArticles = await _articleService.GetUserArticlesAsync(userId.Value);
                ViewBag.Notifications = await _articleService.GetUserNotificationsAsync(userId.Value);
                return View("Index", myArticles);
            }

            // Public Mode (Home / Category).
            //
            // NOTE: previously this used 4 × Task.Run with separate scopes so the
            // queries ran "in parallel". On cold start that opened 4 simultaneous
            // MySQL connections and made the page take ~10s. EF Core I/O is already
            // async, so sequential await on ONE DbContext reuses a single pooled
            // connection and is actually faster in practice. Responses are also
            // cached in-memory inside ArticleService (see AddMemoryCache), so after
            // the first hit subsequent loads skip the DB entirely until the TTL
            // expires or a moderator action invalidates the cache.
            ViewBag.Categories = await _articleService.GetCategoriesAsync();
            ViewBag.Featured = await _articleService.GetFeaturedArticlesAsync(4, categorySlug);
            ViewBag.Latest = await _articleService.GetLatestArticlesAsync(
                string.IsNullOrEmpty(categorySlug) ? 12 : 24, categorySlug);
            ViewBag.MostViewed = await _articleService.GetMostViewedArticlesAsync(10);

            if (string.IsNullOrEmpty(categorySlug))
            {
                ViewBag.Mode = "home";

                // Build per-category latest lists for the home page's section blocks
                // (kiểu báo Đà Nẵng: mỗi chuyên mục một block "header đỏ + 1 bài lớn + 6 bài nhỏ").
                var byCat = new Dictionary<string, List<Article>>();
                var cats = ViewBag.Categories as List<Category> ?? new();
                foreach (var c in cats)
                {
                    if (string.IsNullOrEmpty(c.Slug)) continue;
                    byCat[c.Slug] = await _articleService.GetLatestArticlesAsync(7, c.Slug);
                }
                ViewBag.LatestByCat = byCat;
            }

            return View("Index", new List<Article>());
        }

        // ══════════════════════════════════════════════
        // CHI TIẾT BÀI VIẾT
        // ══════════════════════════════════════════════
        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var article = await _articleService.GetArticleByIdAsync(id);
            if (article == null || article.Status != 2)
                return NotFound();

            // Tăng lượt xem
            var userId = GetCurrentUserId();
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            await _articleService.IncrementViewCountAsync(id, ip, userId);

            ViewBag.Related = await _articleService.GetRelatedArticlesAsync(id, 6);
            ViewBag.MostViewed = await _articleService.GetMostViewedArticlesAsync(5);
            ViewBag.LatestSameCategory = await _articleService.GetLatestArticlesAsync(6, article.Category?.Slug);
            ViewBag.UserId = userId;
            ViewBag.Role = GetCurrentUserRole();

            return View("Details", article);
        }

        // ══════════════════════════════════════════════
        // ĐĂNG BÀI
        // ══════════════════════════════════════════════
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var userId = GetCurrentUserId();
            if (userId == null) return Redirect("/Auth/Login");

            ViewBag.Categories = await _articleService.GetCategoriesAsync();
            ViewBag.Role = GetCurrentUserRole();
            return View("Create");
        }

        [HttpPost]
        public async Task<IActionResult> Create(string title, string summary, string content,
            int categoryId, IFormFile? image, bool isFeatured = false)
        {
            var userId = GetCurrentUserId();
            var role = GetCurrentUserRole();
            if (userId == null) return Unauthorized();

            string? imageUrl = null;
            if (image != null && image.Length > 0)
            {
                var uploadsDir = Path.Combine(_env.WebRootPath, "uploads", "articles");
                Directory.CreateDirectory(uploadsDir);
                var fileName = $"{Guid.NewGuid()}{Path.GetExtension(image.FileName)}";
                var filePath = Path.Combine(uploadsDir, fileName);
                using var stream = new FileStream(filePath, FileMode.Create);
                await image.CopyToAsync(stream);
                imageUrl = $"/uploads/articles/{fileName}";
            }

            var article = new Article
            {
                Title = title,
                Summary = summary,
                Content = content,
                CategoryId = categoryId,
                AuthorId = userId.Value,
                ImageUrl = imageUrl,
                IsFeatured = (role == "Admin") ? isFeatured : false,
                Status = (role == "Admin") ? 2 : 1 // Admin auto approve
            };

            await _articleService.CreateArticleAsync(article);

            if (role == "Admin") return RedirectToAction("Index", new { mode = "admin" });
            return RedirectToAction("Index", new { mode = "my" });
        }

        // ══════════════════════════════════════════════
        // SAFEWIKI
        // ══════════════════════════════════════════════
        [HttpGet]
        public IActionResult SafeWiki()
        {
            ViewBag.UserId = GetCurrentUserId();
            ViewBag.Role = GetCurrentUserRole();
            return View();
        }

        // ══════════════════════════════════════════════
        // HOTLINE KHẨN CẤP
        // ══════════════════════════════════════════════
        [HttpGet]
        public IActionResult Hotline()
        {
            ViewBag.UserId = GetCurrentUserId();
            ViewBag.Role = GetCurrentUserRole();
            return View();
        }

        // ══════════════════════════════════════════════
        // API ENDPOINTS
        // ══════════════════════════════════════════════

        // POST /Article/AddComment
        [HttpPost]
        public async Task<IActionResult> AddComment([FromBody] CommentRequest req)
        {
            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized(new { message = "Vui lòng đăng nhập để bình luận" });

            var comment = await _articleService.AddCommentAsync(req.ArticleId, userId.Value, req.Content);
            return Ok(new
            {
                id = comment.Id,
                content = comment.Content,
                userName = comment.User?.FullName ?? "Ẩn danh",
                avatar = comment.User?.Avatar,
                createdAt = comment.CreatedAt.ToString("dd/MM/yyyy HH:mm")
            });
        }

        // GET /Article/GetNotifications
        [HttpGet]
        public async Task<IActionResult> GetNotifications()
        {
            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized();

            var notifications = await _articleService.GetUserNotificationsAsync(userId.Value);
            var unread = await _articleService.GetUnreadNotificationCountAsync(userId.Value);

            return Ok(new
            {
                unreadCount = unread,
                items = notifications.Select(n => new
                {
                    n.Id,
                    n.Title,
                    n.Content,
                    n.Type,
                    isRead = n.IsRead ?? false,
                    n.ArticleId,
                    createdAt = n.CreatedAt?.ToString("dd/MM/yyyy HH:mm")
                })
            });
        }

        // POST /Article/MarkNotificationRead/{id}
        [HttpPost]
        public async Task<IActionResult> MarkNotificationRead(int id)
        {
            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized();
            await _articleService.MarkNotificationReadAsync(id, userId.Value);
            return Ok();
        }

        // POST /Article/MarkAllRead
        [HttpPost]
        public async Task<IActionResult> MarkAllRead()
        {
            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized();
            await _articleService.MarkAllNotificationsReadAsync(userId.Value);
            return Ok();
        }

        // POST /Article/Delete/{id}
        [HttpPost]
        public async Task<IActionResult> DeleteArticle(int id)
        {
            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized();
            var result = await _articleService.DeleteArticleAsync(id, userId.Value);
            if (!result) return NotFound();
            return Ok();
        }

        // ══════════════════════════════════════════════
        // TINYMCE UPLOAD IMAGE
        // ══════════════════════════════════════════════
        [HttpPost]
        public async Task<IActionResult> UploadImage(IFormFile file)
        {
            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized();

            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded");

            var uploadsDir = Path.Combine(_env.WebRootPath, "uploads", "articles");
            Directory.CreateDirectory(uploadsDir);
            var fileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
            var path = Path.Combine(uploadsDir, fileName);

            using (var stream = new FileStream(path, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            return Json(new { location = $"/uploads/articles/{fileName}" });
        }

        // ══════════════════════════════════════════════
        // ADMIN: DUYỆT BÀI VIẾT
        // ══════════════════════════════════════════════

        [HttpGet]
        public async Task<IActionResult> AdminArticles()
        {
            var role = GetCurrentUserRole();
            if (role != "Admin") return Forbid();

            var pending = await _articleService.GetPendingArticlesAsync();
            return View(pending);
        }

        [HttpPost]
        public async Task<IActionResult> Approve(int id)
        {
            var userId = GetCurrentUserId();
            var role = GetCurrentUserRole();
            if (role != "Admin" || userId == null) return Forbid();

            var success = await _articleService.ApproveArticleAsync(id, userId.Value);
            return success ? Ok() : NotFound();
        }

        [HttpPost]
        public async Task<IActionResult> ToggleFeatured(int id)
        {
            var userId = GetCurrentUserId();
            var role = GetCurrentUserRole();
            if (role != "Admin" || userId == null) return Forbid();

            var success = await _articleService.ToggleFeaturedArticleAsync(id, userId.Value);
            return success ? Ok() : NotFound();
        }

        [HttpPost]
        public async Task<IActionResult> Reject(int id, [FromBody] RejectRequest req)
        {
            var userId = GetCurrentUserId();
            var role = GetCurrentUserRole();
            if (role != "Admin" || userId == null) return Forbid();

            await _articleService.RejectArticleAsync(id, userId.Value, req.Reason);
            return Ok(new { message = "Đã từ chối bài viết" });
        }
    }

    // Request models
    public class CommentRequest
    {
        public int ArticleId { get; set; }
        public string Content { get; set; } = string.Empty;
    }

    public class RejectRequest
    {
        public string Reason { get; set; } = string.Empty;
    }
}
