using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DaNangSafeMap.Models.Entities
{
    [Table("articlecomments")]
    public class ArticleComment
    {
        [Key]
        public int Id { get; set; }

        public int ArticleId { get; set; }

        public int UserId { get; set; }

        [Required]
        public string Content { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation
        [ForeignKey("ArticleId")]
        public Article? Article { get; set; }

        [ForeignKey("UserId")]
        public User? User { get; set; }
    }
}
