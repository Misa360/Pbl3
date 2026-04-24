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

        // ── Bảng Users (đã có) ──
        public DbSet<User> Users { get; set; }

        // ── Bảng Alert (đã có) ──
        public DbSet<AlertCategory> AlertCategories { get; set; }
        public DbSet<AlertType> AlertTypes { get; set; }
        public DbSet<SecurityAlert> SecurityAlerts { get; set; }
        public DbSet<AlertMedia> AlertMedia { get; set; }
        public DbSet<AlertVerification> AlertVerifications { get; set; }

        // ── Bảng Article (MỚI) ──
        public DbSet<Category> Categories { get; set; }
        public DbSet<Article> Articles { get; set; }
        public DbSet<ArticleComment> ArticleComments { get; set; }
        public DbSet<ArticleView> ArticleViews { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<Tag> Tags { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ════════════════════════════════════════════
            // USER
            // ════════════════════════════════════════════
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasIndex(e => e.Email).IsUnique();
                entity.HasIndex(e => e.GoogleId).IsUnique();
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.Property(e => e.Role).HasDefaultValue("User");
                entity.Property(e => e.AuthProvider).HasDefaultValue("Local");
                entity.Property(e => e.IsActive).HasDefaultValue(true);
            });

            // ════════════════════════════════════════════
            // ALERT CATEGORY
            // ════════════════════════════════════════════
            modelBuilder.Entity<AlertCategory>(entity =>
            {
                entity.HasIndex(e => e.Slug).IsUnique();
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.Property(e => e.IsActive).HasDefaultValue(true);
                entity.HasMany(e => e.AlertTypes)
                      .WithOne(t => t.Category)
                      .HasForeignKey(t => t.CategoryId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // ════════════════════════════════════════════
            // ALERT TYPE
            // ════════════════════════════════════════════
            modelBuilder.Entity<AlertType>(entity =>
            {
                entity.HasIndex(e => e.Slug).IsUnique();
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.Property(e => e.IsActive).HasDefaultValue(true);
                entity.HasMany(e => e.SecurityAlerts)
                      .WithOne(a => a.AlertType)
                      .HasForeignKey(a => a.AlertTypeId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // ════════════════════════════════════════════
            // SECURITY ALERT
            // ════════════════════════════════════════════
            modelBuilder.Entity<SecurityAlert>(entity =>
            {
                entity.Property(e => e.Status).HasDefaultValue("PENDING_REVIEW");
                entity.Property(e => e.TrustScore).HasDefaultValue(0);
                entity.Property(e => e.ConfirmCount).HasDefaultValue(0);
                entity.Property(e => e.DenyCount).HasDefaultValue(0);
                entity.Property(e => e.Opacity).HasDefaultValue(30);
                entity.Property(e => e.HasMedia).HasDefaultValue(false);
                entity.Property(e => e.UserConfirmed).HasDefaultValue(false);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.HasOne(e => e.User)
                      .WithMany(u => u.SecurityAlerts)
                      .HasForeignKey(e => e.UserId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasMany(e => e.Media)
                      .WithOne(m => m.Alert)
                      .HasForeignKey(m => m.AlertId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasMany(e => e.Verifications)
                      .WithOne(v => v.Alert)
                      .HasForeignKey(v => v.AlertId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(e => new { e.Latitude, e.Longitude });
                entity.HasIndex(e => new { e.Status, e.CreatedAt });
                entity.HasIndex(e => e.IncidentTime);
                entity.HasIndex(e => e.ExpiresAt);
            });

            // ════════════════════════════════════════════
            // ALERT MEDIA
            // ════════════════════════════════════════════
            modelBuilder.Entity<AlertMedia>(entity =>
            {
                entity.Property(e => e.MediaType).HasDefaultValue("IMAGE");
                entity.Property(e => e.SourceType).HasDefaultValue("ORIGINAL");
                entity.Property(e => e.IsActive).HasDefaultValue(true);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.HasOne(e => e.User)
                      .WithMany()
                      .HasForeignKey(e => e.UserId)
                      .OnDelete(DeleteBehavior.Restrict);
                entity.HasIndex(e => e.AlertId);
            });

            // ════════════════════════════════════════════
            // ALERT VERIFICATION
            // ════════════════════════════════════════════
            modelBuilder.Entity<AlertVerification>(entity =>
            {
                entity.HasIndex(e => new { e.AlertId, e.UserId }).IsUnique();
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.HasOne(e => e.User)
                      .WithMany()
                      .HasForeignKey(e => e.UserId)
                      .OnDelete(DeleteBehavior.Restrict);
                entity.HasIndex(e => new { e.AlertId, e.VerificationType });
            });

            // ════════════════════════════════════════════
            // CATEGORY (Tin tức)
            // ════════════════════════════════════════════
            modelBuilder.Entity<Category>(entity =>
            {
                entity.HasIndex(e => e.Slug).IsUnique();
            });

            // ════════════════════════════════════════════
            // ARTICLE
            // ════════════════════════════════════════════
            modelBuilder.Entity<Article>(entity =>
            {
                entity.HasIndex(e => e.Slug).IsUnique();
                entity.HasIndex(e => e.Status);
                entity.Property(e => e.Status).HasDefaultValue(1);
                entity.Property(e => e.IsFeatured).HasDefaultValue(false);
                entity.Property(e => e.ViewCount).HasDefaultValue(0);
                entity.Property(e => e.CategoryId).HasDefaultValue(1);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.HasOne(e => e.Category)
                      .WithMany(c => c.Articles)
                      .HasForeignKey(e => e.CategoryId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Author)
                      .WithMany(u => u.Articles)
                      .HasForeignKey(e => e.AuthorId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Moderator)
                      .WithMany()
                      .HasForeignKey(e => e.ModeratedBy)
                      .OnDelete(DeleteBehavior.SetNull);
            });

            // ════════════════════════════════════════════
            // ARTICLE COMMENT
            // ════════════════════════════════════════════
            modelBuilder.Entity<ArticleComment>(entity =>
            {
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.HasOne(e => e.Article)
                      .WithMany(a => a.Comments)
                      .HasForeignKey(e => e.ArticleId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.User)
                      .WithMany(u => u.ArticleComments)
                      .HasForeignKey(e => e.UserId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // ════════════════════════════════════════════
            // ARTICLE VIEW
            // ════════════════════════════════════════════
            modelBuilder.Entity<ArticleView>(entity =>
            {
                entity.Property(e => e.ViewedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.HasOne(e => e.Article)
                      .WithMany(a => a.Views)
                      .HasForeignKey(e => e.ArticleId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // ════════════════════════════════════════════
            // NOTIFICATION
            // ════════════════════════════════════════════
            modelBuilder.Entity<Notification>(entity =>
            {
                entity.Property(e => e.IsRead).HasDefaultValue(false);
                entity.Property(e => e.Type).HasDefaultValue("SYSTEM");
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.HasOne(e => e.User)
                      .WithMany(u => u.Notifications)
                      .HasForeignKey(e => e.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Article)
                      .WithMany()
                      .HasForeignKey(e => e.ArticleId)
                      .OnDelete(DeleteBehavior.SetNull);
            });

            // ════════════════════════════════════════════
            // TAG
            // ════════════════════════════════════════════
            modelBuilder.Entity<Tag>(entity =>
            {
                entity.HasIndex(e => e.Slug).IsUnique();
            });
        }
    }
}
