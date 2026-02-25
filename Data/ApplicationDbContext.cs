using Microsoft.EntityFrameworkCore;
using DaNangSafeMap.Models.Entities;

namespace DaNangSafeMap.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Article> Articles { get; set; }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<User>(entity =>
            {
                // Unique constraints
                entity.HasIndex(e => e.Email).IsUnique();
                entity.HasIndex(e => e.GoogleId).IsUnique();

                // Default values
                entity.Property(e => e.CreatedAt)
                      .HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.Property(e => e.Role)
                      .HasDefaultValue("User");

                entity.Property(e => e.AuthProvider)
                      .HasDefaultValue("Local");

                entity.Property(e => e.IsActive)
                      .HasDefaultValue(true);
            });
        }
    }
}