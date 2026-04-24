-- ══════════════════════════════════════════════════════════════════
-- 13_seed_notifications.sql
-- Seed vài thông báo demo cho tất cả user hiện có để test icon chuông
-- (bell) ở navbar.
--
-- Cách chạy:
--   docker exec -i pbl3-mysql mysql -uroot -pBuingocanh@234 DaNangSafeMap \
--       < SQL/13_seed_notifications.sql
--
-- Các thông báo gắn kèm ArticleId (nếu còn) để click mở đúng bài.
-- ══════════════════════════════════════════════════════════════════
USE DaNangSafeMap;

-- Tạo notification cho mỗi user, gán vào 3 bài mới nhất (nếu có).
-- Nếu bạn đã chạy seed trước, script này vẫn an toàn vì lọc trùng theo
-- (UserId, Title, ArticleId).
INSERT INTO notifications (UserId, ArticleId, Title, Content, Type, IsRead, CreatedAt)
SELECT u.Id, a.Id,
       CONCAT('Bài mới: ', a.Title) AS Title,
       COALESCE(a.Summary, 'Có bài viết mới liên quan đến bạn.') AS Content,
       'article_new' AS Type,
       0 AS IsRead,
       NOW() - INTERVAL (ROW_NUMBER() OVER (PARTITION BY u.Id ORDER BY a.CreatedAt DESC)) HOUR AS CreatedAt
FROM Users u
CROSS JOIN (
    SELECT Id, Title, Summary, CreatedAt
    FROM articles
    WHERE Status = 2 AND DeletedAt IS NULL
    ORDER BY CreatedAt DESC
    LIMIT 3
) a
WHERE NOT EXISTS (
    SELECT 1 FROM notifications n
    WHERE n.UserId = u.Id AND n.ArticleId = a.Id
      AND n.Title = CONCAT('Bài mới: ', a.Title)
);

-- Thêm 2 thông báo hệ thống (không gắn Article) cho mỗi user.
INSERT INTO notifications (UserId, ArticleId, Title, Content, Type, IsRead, CreatedAt)
SELECT u.Id, NULL,
       'Chào mừng bạn đến với DaNang SafeMap',
       'Cảm ơn bạn đã sử dụng hệ thống bản đồ an ninh TP Đà Nẵng.',
       'system',
       0,
       NOW() - INTERVAL 30 MINUTE
FROM Users u
WHERE NOT EXISTS (
    SELECT 1 FROM notifications n
    WHERE n.UserId = u.Id AND n.Title = 'Chào mừng bạn đến với DaNang SafeMap'
);

INSERT INTO notifications (UserId, ArticleId, Title, Content, Type, IsRead, CreatedAt)
SELECT u.Id, NULL,
       'Cập nhật chính sách bảo mật',
       'Chúng tôi vừa cập nhật điều khoản sử dụng. Vui lòng xem chi tiết tại trang chính sách.',
       'system',
       0,
       NOW() - INTERVAL 2 HOUR
FROM Users u
WHERE NOT EXISTS (
    SELECT 1 FROM notifications n
    WHERE n.UserId = u.Id AND n.Title = 'Cập nhật chính sách bảo mật'
);
