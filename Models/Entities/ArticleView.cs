using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DaNangSafeMap.Models.Entities
{
    [Table("article_views")]
    public class ArticleView
    {
        [Key]
        public int Id { get; set; }

        public int ArticleId { get; set; }

        public int? UserId { get; set; }

        [MaxLength(45)]
        public string? IpAddress { get; set; }

        public DateTime? ViewedAt { get; set; } = DateTime.Now;

        // Navigation
        [ForeignKey("ArticleId")]
        public Article? Article { get; set; }
    }
}
