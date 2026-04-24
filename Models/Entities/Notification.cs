using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DaNangSafeMap.Models.Entities
{
    [Table("notifications")]
    public class Notification
    {
        [Key]
        public int Id { get; set; }

        public int UserId { get; set; }

        public int? ArticleId { get; set; }

        [Required]
        [MaxLength(255)]
        public string Title { get; set; } = string.Empty;

        [Required]
        public string Content { get; set; } = string.Empty;

        /// <summary>
        /// APPROVED, REJECTED, COMMENT, SYSTEM
        /// </summary>
        [MaxLength(50)]
        public string? Type { get; set; } = "SYSTEM";

        public bool? IsRead { get; set; } = false;

        public DateTime? CreatedAt { get; set; } = DateTime.Now;

        // Navigation
        [ForeignKey("UserId")]
        public User? User { get; set; }

        [ForeignKey("ArticleId")]
        public Article? Article { get; set; }
    }
}
