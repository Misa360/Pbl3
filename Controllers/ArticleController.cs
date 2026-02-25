using DaNangSafeMap.Models.Entities;
using DaNangSafeMap.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Security.Claims;
using System.Text.RegularExpressions;
using System.Text;

namespace DaNangSafeMap.Controllers
{
    public class ArticleController : Controller
    {
        private readonly IArticleService _articleService;
        private readonly IWebHostEnvironment _hostEnvironment;

        public ArticleController(IArticleService articleService, IWebHostEnvironment hostEnvironment)
        {
            _articleService = articleService;
            _hostEnvironment = hostEnvironment;
        }

        // 1. Trang chủ tin tức: Chỉ hiển thị bài đã được duyệt (Status = 2)
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var articles = await _articleService.GetAllArticlesAsync();
            // Lọc các bài viết có trạng thái xuất bản và chưa bị xóa mềm
            var publishedArticles = articles.Where(x => x.Status == 2 && x.DeletedAt == null);
            return View(publishedArticles);
        }

        // 2. Chi tiết bài viết
        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var article = await _articleService.GetArticleByIdAsync(id);
            if (article == null || article.DeletedAt != null) return NotFound();

            // Tăng lượt xem dùng Session để tránh F5 tăng view ảo
            string sessionKey = $"Viewed_{id}";
            if (string.IsNullOrEmpty(HttpContext.Session.GetString(sessionKey)))
            {
                article.ViewCount++;
                await _articleService.UpdateArticleAsync(article);
                HttpContext.Session.SetString(sessionKey, "1");
            }

            // Lấy bài liên quan cùng chuyên mục để hiển thị sidebar ĐỌC TIếP
            var related = await _articleService.GetRelatedArticlesAsync(article.CategoryId, id, 6);
            ViewBag.RelatedArticles = related;

            return View(article);
        }

        // 3. API Infinite Scroll: Tải thêm tin tức
        [HttpGet]
        public async Task<IActionResult> GetMoreArticles(int page = 2)
        {
            int pageSize = 10;
            var allArticles = await _articleService.GetAllArticlesAsync();
            var moreArticles = allArticles
                .Where(x => x.Status == 2 && x.DeletedAt == null)
                .OrderByDescending(x => x.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            if (!moreArticles.Any()) return Content("");

            return PartialView("_ArticleSection", moreArticles);
        }

        // 4. Soạn thảo tin tức (GET): Phải đăng nhập mới vào được
        [Authorize] 
        [HttpGet]
        public IActionResult Create()
        {
            ViewBag.Categories = GetCategoryList();
            return View();
        }

        // 5. Lưu bài viết (POST)
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Article article, IFormFile? imageFile, bool isSpotlight = false)
        {
            // Loại bỏ các trường hệ thống khỏi Validate
            ModelState.Remove("AuthorId");
            ModelState.Remove("ImageUrl");
            ModelState.Remove("Slug");
            ModelState.Remove("Author");
            ModelState.Remove("DeletedAt");

            if (ModelState.IsValid)
            {
                article.Slug = GenerateSlug(article.Title);

                // Xử lý upload ảnh (tùy chọn)
                if (imageFile != null && imageFile.Length > 0)
                {
                    string fileName = Guid.NewGuid().ToString() + Path.GetExtension(imageFile.FileName);
                    string uploadDir = Path.Combine(_hostEnvironment.WebRootPath, "uploads");
                    if (!Directory.Exists(uploadDir)) Directory.CreateDirectory(uploadDir);
                    string filePath = Path.Combine(uploadDir, fileName);
                    using (var fs = new FileStream(filePath, FileMode.Create))
                        await imageFile.CopyToAsync(fs);
                    article.ImageUrl = "/uploads/" + fileName;
                }

                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim == null)
                {
                    ModelState.AddModelError("", "Vui lòng đăng nhập để đăng bài.");
                    ViewBag.Categories = GetCategoryList();
                    return View(article);
                }

                article.AuthorId = int.Parse(userIdClaim.Value);
                article.CreatedAt = DateTime.Now;
                article.ViewCount = 0;
                article.IsFeatured = isSpotlight;
                // Admin → Status=2 (xuất bản ngay), User → Status=1 (chờ duyệt)
                article.Status = User.IsInRole("Admin") ? 2 : 1;

                var result = await _articleService.CreateArticleAsync(article);
                if (result)
                {
                    TempData["Success"] = article.Status == 2
                        ? "Đăng bài thành công!"
                        : "Bài viết đã gửi, vui lòng chờ Admin duyệt.";
                    return RedirectToAction(nameof(Index));
                }
            }

            ViewBag.Categories = GetCategoryList();
            return View(article);
        }

        // 6. Logic Kiểm duyệt (Dành cho Quản trị viên)
        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<IActionResult> Moderate(int id, int newStatus, string? note)
        {
            var article = await _articleService.GetArticleByIdAsync(id);
            if (article == null) return NotFound();

            var adminId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

            article.Status = newStatus; // 2: Duyệt, 3: Từ chối
            article.ModeratedBy = adminId;
            article.ModeratedAt = DateTime.Now;
            
            if (newStatus == 3) article.RejectReason = note;

            await _articleService.UpdateArticleAsync(article);
            TempData["Success"] = newStatus == 2 ? "Đã duyệt bài viết!" : "Đã từ chối bài viết.";
            
            return RedirectToAction(nameof(Index));
        }

        // 7. Xóa bài viết (Sử dụng Soft Delete)
        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var article = await _articleService.GetArticleByIdAsync(id);
            if (article != null)
            {
                article.DeletedAt = DateTime.Now; // Gán ngày xóa thay vì Remove khỏi DB
                await _articleService.UpdateArticleAsync(article);
                TempData["Success"] = "Xóa bài viết thành công!";
            }
            return RedirectToAction(nameof(Index));
        }

        // Danh sách chuyên mục cố định
        private List<SelectListItem> GetCategoryList()
        {
            return new List<SelectListItem>
            {
                new SelectListItem { Value = "1", Text = "🚨 Cảnh báo tội phạm" },
                new SelectListItem { Value = "2", Text = "🔍 Tìm kiếm người thân" },
                new SelectListItem { Value = "3", Text = "📢 Tin tức cộng đồng" }
            };
        }

        // Hàm xử lý tạo Slug tiếng Việt không dấu
        private string GenerateSlug(string phrase)
        {
            string str = phrase.ToLower();
            str = Regex.Replace(str, @"[áàảãạâấầẩẫậăắằẳẵặ]", "a");
            str = Regex.Replace(str, @"[éèẻẽẹêếềểễệ]", "e");
            str = Regex.Replace(str, @"[íìỉĩị]", "i");
            str = Regex.Replace(str, @"[óòỏõọôốồổỗộơớờởỡợ]", "o");
            str = Regex.Replace(str, @"[úùủũụưứừửữự]", "u");
            str = Regex.Replace(str, @"[ýỳỷỹỵ]", "y");
            str = Regex.Replace(str, @"đ", "d");
            str = Regex.Replace(str, @"[^a-z0-9\s-]", "");
            str = Regex.Replace(str, @"\s+", "-").Trim();
            return str.Length > 200 ? str.Substring(0, 200) : str;
        }
    }
}