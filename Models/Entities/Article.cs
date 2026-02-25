using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DaNangSafeMap.Models.Entities
{
    public class Article
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập tiêu đề")]
        public string Title { get; set; } = string.Empty;

        // CẦN CHỈNH: Thêm ? vì dữ liệu cũ có thể NULL cột này
        public string? Slug { get; set; } 

        [Required(ErrorMessage = "Vui lòng nhập tóm tắt")]
        public string Summary { get; set; } = string.Empty;

        [Required(ErrorMessage = "Nội dung không được để trống")]
        public string Content { get; set; } = string.Empty;

        // CẦN CHỈNH: Thêm ? 
        public string? ImageUrl { get; set; }

        public int CategoryId { get; set; } 
        public int Status { get; set; } = 1; 

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? UpdatedAt { get; set; }

        public int AuthorId { get; set; }

        [ForeignKey("AuthorId")]
        public virtual User? Author { get; set; } 

        // CÁC CỘT KIỂM DUYỆT MỚI (Dựa trên ảnh cấu trúc gộp của bạn)
        public int? ModeratedBy { get; set; } 
        public DateTime? ModeratedAt { get; set; }
        
        // CẦN CHỈNH: Thêm ? vì bài mới chưa bị từ chối sẽ NULL cột này
        public string? RejectReason { get; set; } 

        public bool IsFeatured { get; set; } = false;
        public int ViewCount { get; set; } = 0; 

        // CỘT XÓA MỀM (Phải có để khớp với ArticleService)
        public DateTime? DeletedAt { get; set; }
    }
}