-- ══════════════════════════════════════════════════════════════════
-- 11_seed_article_categories.sql
-- Seed 2 chuyên mục tin tức dùng ở /Article (An ninh, Đời sống).
-- Idempotent — INSERT IGNORE theo Slug.
-- ══════════════════════════════════════════════════════════════════
USE DaNangSafeMap;

-- Đảm bảo bảng có cột Slug là UNIQUE để INSERT IGNORE làm việc.
-- Nếu đã có dữ liệu khác slug, câu lệnh này sẽ không ghi đè.
INSERT IGNORE INTO categories (Slug, Name, `Order`, IsActive, CreatedAt)
VALUES
    ('an-ninh',  'An ninh – Trật tự', 1, 1, NOW()),
    ('doi-song', 'Đời sống đô thị',   2, 1, NOW());
