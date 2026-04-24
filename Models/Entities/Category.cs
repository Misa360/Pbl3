using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DaNangSafeMap.Models.Entities
{
    [Table("categories")]
    public class Category
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(255)]
        public string? Slug { get; set; }

        [MaxLength(500)]
        public string? Description { get; set; }

        // Navigation
        public ICollection<Article>? Articles { get; set; }
    }
}
