-- ══════════════════════════════════════════════════════════════════
-- 12_seed_articles.sql
-- Seed 12 bài viết demo dùng cho trang /Article.
--
-- Cách chạy:
--   docker exec -i pbl3-mysql mysql -uroot -pBuingocanh@234 DaNangSafeMap \
--       < SQL/12_seed_articles.sql
--
-- Script phụ thuộc: 11_seed_article_categories.sql (cần 2 chuyên mục
-- 'an-ninh' và 'doi-song') + một Admin user (04_admin.sql).
--
-- Idempotent — dùng INSERT IGNORE theo Slug.
-- ══════════════════════════════════════════════════════════════════
USE DaNangSafeMap;

SET @cat_an_ninh  = (SELECT Id FROM categories WHERE Slug = 'an-ninh'  LIMIT 1);
SET @cat_doi_song = (SELECT Id FROM categories WHERE Slug = 'doi-song' LIMIT 1);
SET @admin_id     = (SELECT Id FROM Users      WHERE Role  = 'Admin'   LIMIT 1);

INSERT IGNORE INTO articles
    (Title, Slug, Summary, Content, ImageUrl, CategoryId, AuthorId,
     CreatedAt, Status, IsFeatured, ViewCount)
VALUES
    ('Công an Đà Nẵng triệt phá đường dây lừa đảo qua mạng xã hội',
     'cong-an-da-nang-triet-pha-duong-day-lua-dao-qua-mang-xa-hoi',
     'Lực lượng chức năng TP Đà Nẵng vừa bắt giữ nhóm đối tượng chuyên giả danh công an để lừa đảo chuyển khoản, thu giữ hàng trăm triệu đồng.',
     '<p>Chi tiết vụ án...</p>',
     'https://images.unsplash.com/photo-1451187580459-43490279c0fa?w=1200',
     @cat_an_ninh, @admin_id,
     NOW() - INTERVAL 10 HOUR, 2, 1, 120),

    ('Cảnh báo tình trạng cướp giật tại các tuyến đường ven biển',
     'canh-bao-tinh-trang-cuop-giat-tai-cac-tuyen-duong-ven-bien',
     'Gần đây xuất hiện nhiều vụ cướp giật tài sản du khách tại khu vực ven biển. Công an khuyến cáo du khách bảo quản tư trang, không đeo trang sức giá trị khi đi dạo tối.',
     '<p>Chi tiết cảnh báo...</p>',
     'https://images.unsplash.com/photo-1449034446853-66c86144b0ad?w=1200',
     @cat_an_ninh, @admin_id,
     NOW() - INTERVAL 1 DAY,   2, 1, 85),

    ('Phát hiện kho hàng nhái quy mô lớn tại quận Hải Châu',
     'phat-hien-kho-hang-nhai-quy-mo-lon-tai-quan-hai-chau',
     'Cơ quan quản lý thị trường phối hợp công an phát hiện kho chứa hàng ngàn sản phẩm nghi nhái thương hiệu nổi tiếng tại một địa điểm ở quận Hải Châu.',
     '<p>Chi tiết vụ việc...</p>',
     'https://images.unsplash.com/photo-1486406146926-c627a92ad1ab?w=1200',
     @cat_an_ninh, @admin_id,
     NOW() - INTERVAL 2 DAY,   2, 0, 64),

    ('Đà Nẵng tăng cường tuần tra đêm sau vụ va chạm giao thông nghiêm trọng',
     'da-nang-tang-cuong-tuan-tra-dem',
     'Sau vụ tai nạn trên quốc lộ 1A, CSGT Đà Nẵng bổ sung lực lượng tuần tra khung giờ 22h-5h nhằm xử lý phương tiện chạy quá tốc độ.',
     '<p>Chi tiết...</p>',
     'https://images.unsplash.com/photo-1520340356584-f9917d1eea6f?w=1200',
     @cat_an_ninh, @admin_id,
     NOW() - INTERVAL 3 DAY,   2, 0, 48),

    ('Nhiều tuyến phố đi bộ mới khai trương thu hút du khách',
     'nhieu-tuyen-pho-di-bo-moi-khai-truong',
     'Các tuyến phố đi bộ dọc sông Hàn được đưa vào khai thác dịp cuối tuần với hàng trăm gian hàng ẩm thực và âm nhạc đường phố.',
     '<p>Chi tiết...</p>',
     'https://images.unsplash.com/photo-1449824913935-59a10b8d2000?w=1200',
     @cat_doi_song, @admin_id,
     NOW() - INTERVAL 4 DAY,   2, 1, 210),

    ('Chương trình thu gom rác thải nhựa tại bãi biển Mỹ Khê',
     'chuong-trinh-thu-gom-rac-thai-nhua-my-khe',
     'Hàng trăm tình nguyện viên tham gia nhặt rác ven biển sáng cuối tuần, thu gom hơn 2 tấn rác thải nhựa chỉ trong một buổi.',
     '<p>Chi tiết...</p>',
     'https://images.unsplash.com/photo-1618477461853-cf6ed80faba5?w=1200',
     @cat_doi_song, @admin_id,
     NOW() - INTERVAL 5 DAY,   2, 0, 96),

    ('Đà Nẵng ra mắt ứng dụng phản ánh đô thị cho người dân',
     'da-nang-ra-mat-ung-dung-phan-anh-do-thi',
     'Ứng dụng cho phép người dân gửi ảnh, video phản ánh các vấn đề đô thị trực tiếp tới cơ quan chức năng và theo dõi tiến độ xử lý.',
     '<p>Chi tiết...</p>',
     'https://images.unsplash.com/photo-1573497019940-1c28c88b4f3e?w=1200',
     @cat_doi_song, @admin_id,
     NOW() - INTERVAL 6 DAY,   2, 0, 78),

    ('Lễ hội pháo hoa 2026: những điều người dân cần lưu ý',
     'le-hoi-phao-hoa-2026-luu-y',
     'Ban tổ chức công bố chi tiết lịch trình, điểm đỗ xe và các tuyến đường cấm để người dân chủ động di chuyển trong các đêm diễn.',
     '<p>Chi tiết...</p>',
     'https://images.unsplash.com/photo-1519638831568-d9897f54ed69?w=1200',
     @cat_doi_song, @admin_id,
     NOW() - INTERVAL 7 DAY,   2, 0, 132),

    ('Truy bắt đối tượng đột nhập nhà dân trong đêm',
     'truy-bat-doi-tuong-dot-nhap-nha-dan',
     'Nhờ hệ thống camera an ninh khu phố, cảnh sát nhanh chóng xác định và bắt giữ nghi phạm trong vòng 6 giờ.',
     '<p>Chi tiết...</p>',
     'https://images.unsplash.com/photo-1504711434969-e33886168f5c?w=1200',
     @cat_an_ninh, @admin_id,
     NOW() - INTERVAL 8 DAY,   2, 0, 55),

    ('Khởi động chương trình đào tạo kỹ năng thoát hiểm cho học sinh',
     'dao-tao-ky-nang-thoat-hiem-cho-hoc-sinh',
     'Chương trình tổ chức tại các trường tiểu học và THCS, trang bị kỹ năng xử lý cháy nổ, đuối nước và tình huống khẩn cấp.',
     '<p>Chi tiết...</p>',
     'https://images.unsplash.com/photo-1577896851231-70ef18881754?w=1200',
     @cat_doi_song, @admin_id,
     NOW() - INTERVAL 9 DAY,   2, 0, 67),

    ('Thời tiết cuối tuần: có mưa rào và dông ở miền Trung',
     'thoi-tiet-cuoi-tuan-mua-rao',
     'Dự báo thời tiết cho biết khu vực miền Trung có mưa rào rải rác, nhiệt độ giảm nhẹ. Người dân chuẩn bị áo mưa khi ra đường.',
     '<p>Chi tiết...</p>',
     'https://images.unsplash.com/photo-1527482797697-8795b05a13fe?w=1200',
     @cat_doi_song, @admin_id,
     NOW() - INTERVAL 10 DAY,  2, 0, 40),

    ('Cảnh báo lừa đảo tuyển cộng tác viên online',
     'canh-bao-lua-dao-tuyen-cong-tac-vien-online',
     'Nhiều người dân báo bị chiếm đoạt tài sản sau khi tham gia nhiệm vụ nạp tiền trên các nền tảng giả mạo sàn thương mại điện tử.',
     '<p>Chi tiết...</p>',
     'https://images.unsplash.com/photo-1587614382346-4ec70e388b28?w=1200',
     @cat_an_ninh, @admin_id,
     NOW() - INTERVAL 14 DAY,  2, 0, 89);
