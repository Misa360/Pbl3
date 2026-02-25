using DaNangSafeMap.Data;
using DaNangSafeMap.Models.Entities;
using DaNangSafeMap.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DaNangSafeMap.Services.Implementations
{
    public class ArticleService : IArticleService
    {
        private readonly ApplicationDbContext _context;

        public ArticleService(ApplicationDbContext context)
        {
            _context = context;
        }

        // 1. Lấy tất cả bài viết chưa bị xóa
        public async Task<IEnumerable<Article>> GetAllArticlesAsync()
        {
            return await _context.Articles
                .Include(a => a.Author)
                .Where(a => a.DeletedAt == null) // Loại bỏ các bài đã xóa mềm
                .OrderByDescending(a => a.CreatedAt)
                .ToListAsync();
        }

        // 2. Lấy chi tiết bài viết theo ID
        public async Task<Article?> GetArticleByIdAsync(int id)
        {
            return await _context.Articles
                .Include(a => a.Author)
                .FirstOrDefaultAsync(a => a.Id == id && a.DeletedAt == null); //
        }

        // 3. Tạo mới bài viết
        public async Task<bool> CreateArticleAsync(Article article)
        {
            article.Slug ??= "";
            // Chỉ gán ảnh mặc định nếu chưa có ảnh nào
            if (string.IsNullOrEmpty(article.ImageUrl))
                article.ImageUrl = null;

            _context.Articles.Add(article);
            return await _context.SaveChangesAsync() > 0;
        }

        // 4. Cập nhật bài viết (Bao gồm cả logic Kiểm duyệt)
        public async Task<bool> UpdateArticleAsync(Article article)
        {
            var existingArticle = await _context.Articles.FindAsync(article.Id);
            if (existingArticle == null) return false;

            // Cập nhật thông tin nội dung
            existingArticle.Title = article.Title;
            existingArticle.Summary = article.Summary;
            existingArticle.Content = article.Content;
            existingArticle.ImageUrl = article.ImageUrl;
            existingArticle.Slug = article.Slug; // Cập nhật Slug mới
            existingArticle.CategoryId = article.CategoryId;
            
            // Cập nhật trạng thái và tiêu điểm
            existingArticle.Status = article.Status;
            existingArticle.IsFeatured = article.IsFeatured; //
            existingArticle.ViewCount = article.ViewCount; // Lưu lượt xem mới

            // Cập nhật thông tin kiểm duyệt (Dùng khi Admin Duyệt/Từ chối)
            existingArticle.ModeratedBy = article.ModeratedBy; //
            existingArticle.ModeratedAt = article.ModeratedAt; //
            existingArticle.RejectReason = article.RejectReason; //

            // Lưu vết thời gian cập nhật
            existingArticle.UpdatedAt = DateTime.Now; //

            _context.Articles.Update(existingArticle);
            return await _context.SaveChangesAsync() > 0;
        }

        // 5. Xóa mềm bài viết
        public async Task<bool> DeleteArticleAsync(int id)
        {
            var article = await _context.Articles.FindAsync(id);
            if (article == null) return false;
            article.DeletedAt = DateTime.Now;
            _context.Articles.Update(article);
            return await _context.SaveChangesAsync() > 0;
        }

        // 6. Lấy bài viết liên quan cùng chuyên mục
        public async Task<List<Article>> GetRelatedArticlesAsync(int categoryId, int currentArticleId, int count)
        {
            return await _context.Articles
                .Include(a => a.Author)
                .Where(a => a.CategoryId == categoryId
                         && a.Id != currentArticleId
                         && a.Status == 2
                         && a.DeletedAt == null)
                .OrderByDescending(a => a.CreatedAt)
                .Take(count)
                .ToListAsync();
        }
    }
}