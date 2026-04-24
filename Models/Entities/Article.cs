using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DaNangSafeMap.Models.Entities
{
    [Table("articles")]
    public class Article
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(255)]
        public string Title { get; set; } = string.Empty;

        [MaxLength(255)]
        public string? Slug { get; set; }

        [MaxLength(500)]
        public string? Summary { get; set; }

        [Required]
        public string Content { get; set; } = string.Empty;

        [MaxLength(255)]
        public string? ImageUrl { get; set; }

        public int CategoryId { get; set; } = 1;

        public int AuthorId { get; set; }

        public int? ModeratedBy { get; set; }

        public DateTime? ModeratedAt { get; set; }

        public DateTime? CreatedAt { get; set; } = DateTime.Now;

        public DateTime? UpdatedAt { get; set; } = DateTime.Now;

        /// <summary>
        /// 1 = PENDING, 2 = APPROVED, 3 = REJECTED
        /// </summary>
        public int? Status { get; set; } = 1;

        public bool? IsFeatured { get; set; } = false;

        public int? ViewCount { get; set; } = 0;

        public string? RejectReason { get; set; }

        public DateTime? DeletedAt { get; set; }

        public string? VideoUrl { get; set; }

        public string? AudioUrl { get; set; }

        [MaxLength(255)]
        public string? SubCategoryName { get; set; }

        // Navigation
        [ForeignKey("CategoryId")]
        public Category? Category { get; set; }

        [ForeignKey("AuthorId")]
        public User? Author { get; set; }

        [ForeignKey("ModeratedBy")]
        public User? Moderator { get; set; }

        public ICollection<ArticleComment>? Comments { get; set; }
        public ICollection<ArticleView>? Views { get; set; }
    }
}
