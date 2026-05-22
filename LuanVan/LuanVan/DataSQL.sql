USE LV2026
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
SET ANSI_PADDING ON
GO
SET ANSI_WARNINGS ON
GO
SET ARITHABORT ON
GO
SET CONCAT_NULL_YIELDS_NULL ON
GO
SET NUMERIC_ROUNDABORT OFF
GO

/* =========================
0. DỌN DỮ LIỆU CŨ + RESET IDENTITY
========================= */

DECLARE @cmd nvarchar(max) = N'';

SELECT @cmd = @cmd + N'ALTER TABLE ' + QUOTENAME(s.name) + N'.' + QUOTENAME(t.name) + N' NOCHECK CONSTRAINT ALL;' + CHAR(10)
FROM sys.tables t
JOIN sys.schemas s ON s.schema_id = t.schema_id
WHERE t.is_ms_shipped = 0;
EXEC sp_executesql @cmd;

SET @cmd = N'';
SELECT @cmd = @cmd + N'DELETE FROM ' + QUOTENAME(s.name) + N'.' + QUOTENAME(t.name) + N';' + CHAR(10)
FROM sys.tables t
JOIN sys.schemas s ON s.schema_id = t.schema_id
WHERE t.is_ms_shipped = 0;
EXEC sp_executesql @cmd;

SET @cmd = N'';
SELECT @cmd = @cmd + N'DBCC CHECKIDENT (''' + QUOTENAME(s.name) + N'.' + QUOTENAME(t.name) + N''', RESEED, 0) WITH NO_INFOMSGS;' + CHAR(10)
FROM sys.tables t
JOIN sys.schemas s ON s.schema_id = t.schema_id
JOIN sys.identity_columns ic ON ic.object_id = t.object_id
WHERE t.is_ms_shipped = 0;
EXEC sp_executesql @cmd;

SET @cmd = N'';
SELECT @cmd = @cmd + N'ALTER TABLE ' + QUOTENAME(s.name) + N'.' + QUOTENAME(t.name) + N' WITH CHECK CHECK CONSTRAINT ALL;' + CHAR(10)
FROM sys.tables t
JOIN sys.schemas s ON s.schema_id = t.schema_id
WHERE t.is_ms_shipped = 0;
EXEC sp_executesql @cmd;
GO

/* =========================
0.1. TẠM TẮT FK VÒNG PHÒNG BAN <-> NHÂN VIÊN
========================= */

ALTER TABLE PHONGBAN NOCHECK CONSTRAINT FK_PHONGBAN_NV_TRUONG_NHANVIEN;
ALTER TABLE NHANVIEN NOCHECK CONSTRAINT FK_NHANVIEN_NV_TRUONG_PHONGBAN;
GO

/* =========================
1. DANH MỤC
========================= */

-- Độ ưu tiên
INSERT INTO DOUUTIEN VALUES (1, N'Thấp');
INSERT INTO DOUUTIEN VALUES (2, N'Trung bình');
INSERT INTO DOUUTIEN VALUES (3, N'Cao');

-- Độ khó
INSERT INTO DOKHO VALUES (1, N'Dễ');
INSERT INTO DOKHO VALUES (2, N'Trung bình');
INSERT INTO DOKHO VALUES (3, N'Khó');

-- Trạng thái công việc
INSERT INTO TRANGTHAICONGVIEC VALUES (1, N'Chưa bắt đầu');
INSERT INTO TRANGTHAICONGVIEC VALUES (2, N'Đang thực hiện');
INSERT INTO TRANGTHAICONGVIEC VALUES (3, N'Hoàn thành');
INSERT INTO TRANGTHAICONGVIEC VALUES (4, N'Trễ hạn');

-- Loại KPI
INSERT INTO LOAIKPI VALUES (1, N'Hiệu suất');
INSERT INTO LOAIKPI VALUES (2, N'Chất lượng');
INSERT INTO LOAIKPI VALUES (3, N'Tiến độ');


/* =========================
2. PHÒNG BAN (NULL trước để tránh FK)
========================= */

INSERT INTO PHONGBAN VALUES (1, 1, N'Phòng IT', N'Phát triển hệ thống', NULL);
INSERT INTO PHONGBAN VALUES (2, 2, N'Phòng Marketing', N'Tiếp thị sản phẩm', NULL);


/* =========================
3. NHÂN VIÊN
========================= */

INSERT INTO NHANVIEN (MANHANVIEN, MAPHONGBAN, PHO_MAPHONGBAN, HOTEN, NGAYSINH, CCCD, DIACHI, GIOITINH, EMAIL, SDT, NGAYVAOLAM, TRANGTHAI)
VALUES (1, 1, 1, N'Nguyễn Văn A', '1995-01-01', '123456789', N'Cần Thơ', N'Nam', 'a@gmail.com', '0900000001', GETDATE(), 1);

INSERT INTO NHANVIEN (MANHANVIEN, MAPHONGBAN, PHO_MAPHONGBAN, HOTEN, NGAYSINH, CCCD, DIACHI, GIOITINH, EMAIL, SDT, NGAYVAOLAM, TRANGTHAI)
VALUES (2, 2, 2, N'Trần Thị B', '1997-02-02', '123456788', N'Cần Thơ', N'Nữ', 'b@gmail.com', '0900000002', GETDATE(), 1);

INSERT INTO NHANVIEN (MANHANVIEN, MAPHONGBAN, PHO_MAPHONGBAN, HOTEN, NGAYSINH, CCCD, DIACHI, GIOITINH, EMAIL, SDT, NGAYVAOLAM, TRANGTHAI)
VALUES (3, 1, 1, N'Lê Văn C', '1998-03-03', '123456787', N'Cần Thơ', N'Nam', 'c@gmail.com', '0900000003', GETDATE(), 1);


/* =========================
4. UPDATE TRƯỞNG PHÒNG
========================= */

UPDATE PHONGBAN SET MANHANVIEN = 1, MATRUONGPHONG = 1 WHERE MAPHONGBAN = 1;
UPDATE PHONGBAN SET MANHANVIEN = 2, MATRUONGPHONG = 2 WHERE MAPHONGBAN = 2;

ALTER TABLE PHONGBAN WITH CHECK CHECK CONSTRAINT FK_PHONGBAN_NV_TRUONG_NHANVIEN;
ALTER TABLE NHANVIEN WITH CHECK CHECK CONSTRAINT FK_NHANVIEN_NV_TRUONG_PHONGBAN;


/* =========================
5. NHÓM
========================= */

INSERT INTO NHOM VALUES (1, N'Team Backend', GETDATE(), 1);
INSERT INTO NHOM VALUES (2, N'Team Marketing', GETDATE(), 2);

-- Thành viên nhóm
INSERT INTO THANHVIENNHOM VALUES (1,1,GETDATE(),N'Trưởng nhóm');
INSERT INTO THANHVIENNHOM VALUES (3,1,GETDATE(),N'Thành viên');
INSERT INTO THANHVIENNHOM VALUES (2,2,GETDATE(),N'Trưởng nhóm');


/* =========================
6. DỰ ÁN
========================= */

INSERT INTO DUAN VALUES 
(1, N'Hệ thống quản lý KPI', N'Xây dựng web AI', '2026-01-01', '2026-06-01', 1),
(2, N'Website bán hàng', N'Triển khai ecommerce', '2026-02-01', '2026-07-01', 1);


/* =========================
7. CÔNG VIỆC
========================= */

INSERT INTO CONGVIEC VALUES 
(1,3,2,2,NULL,1,NULL,N'Thiết kế database',N'Thiết kế ERD', '2026-04-01', 8.5,1),
(2,2,1,1,NULL,1,NULL,N'Thiết kế UI',N'Figma UI', '2026-04-10', 7.0,1),
(3,3,3,2,NULL,2,NULL,N'Xây dựng API',N'Backend ASP.NET', '2026-04-15', 9.0,1);


/* =========================
8. PHÂN CÔNG
========================= */

INSERT INTO PHANCONGNHANVIEN VALUES 
(1,1,1,GETDATE(),'2026-03-01','2026-04-01','2026-03-02',NULL),
(2,2,2,GETDATE(),'2026-03-05','2026-04-10','2026-03-06',NULL),
(3,3,3,GETDATE(),'2026-03-10','2026-04-15','2026-03-11',NULL);


/* =========================
9. KPI
========================= */

-- Danh mục KPI
INSERT INTO DANHMUCKPI VALUES 
(1,1,N'Hoàn thành công việc',0.5),
(2,2,N'Chất lượng công việc',0.3),
(3,3,N'Đúng hạn',0.2);

-- Kết quả KPI
INSERT INTO KETQUAKPI VALUES
(1,1,1,8.5,3,2026),
(2,1,2,7.5,3,2026),
(3,2,1,6.0,3,2026),
(4,3,1,9.0,3,2026);

-- KPI_DOITUONG da ngung su dung, he thong hien tai su dung KPI_NHANVIEN/KPI_DUAN/KPI_NHOM/KPI_PHONGBAN.


/* =========================
10. AI
========================= */

-- Mô hình AI
INSERT INTO MOHINHAI VALUES
(1,N'Linear Regression','v1',GETDATE()),
(2,N'Random Forest','v1',GETDATE());

-- Dữ liệu huấn luyện AI
INSERT INTO DULIEUAI VALUES
(1,1,10,2,5.5,8.0),
(2,2,8,3,6.0,7.0),
(3,3,15,1,4.0,9.0);

-- Dự đoán AI
INSERT INTO DUDOANAI VALUES
(1,1,1,3,2026,8.2,0.2,N'Cải thiện tiến độ',N'Tăng nhân lực',GETDATE()),
(2,2,1,3,2026,6.5,0.5,N'Cần training',N'Giảm tải công việc',GETDATE()),
(3,3,2,3,2026,9.0,0.1,N'Hiệu suất cao',N'Giữ nguyên',GETDATE());


/* =========================
11. KỸ NĂNG
========================= */

INSERT INTO KYNANG VALUES (1, N'C#');
INSERT INTO KYNANG VALUES (2, N'SQL Server');
INSERT INTO KYNANG VALUES (3, N'ReactJS');
INSERT INTO KYNANG VALUES (4, N'Marketing');

-- Kỹ năng nhân viên
INSERT INTO KYNANGNHANVIEN VALUES (1,1,5,3);
INSERT INTO KYNANGNHANVIEN VALUES (2,1,4,2);
INSERT INTO KYNANGNHANVIEN VALUES (3,3,4,2);
INSERT INTO KYNANGNHANVIEN VALUES (4,2,5,5);


/* =========================
12. TÀI LIỆU
========================= */

INSERT INTO TAILIEU VALUES (1, N'Tài liệu thiết kế DB', N'ERD + SQL');
INSERT INTO TAILIEU VALUES (2, N'Tài liệu UI', N'Figma design');

-- Tài liệu công việc
INSERT INTO CONGVIEC_TAILIEU VALUES (1,1);
INSERT INTO CONGVIEC_TAILIEU VALUES (2,2);


/* =========================
13. LOG + CHAT
========================= */

INSERT INTO BINHLUAN VALUES (1,1,1,N'Đang làm phần DB',GETDATE());
INSERT INTO BINHLUAN VALUES (2,1,3,N'Cần hỗ trợ thêm',GETDATE());
INSERT INTO BINHLUAN VALUES (3,2,2,N'UI gần xong',GETDATE());

--Nhật ký công việc
INSERT INTO NHATKYCONGVIEC VALUES 
(1,1,30,N'Đã thiết kế bảng',GETDATE()),
(2,1,70,N'Hoàn thành gần xong',GETDATE()),
(3,2,40,N'Đang làm UI',GETDATE());

--Lịch sử trạng thái công việc
INSERT INTO LICHSUTRANGTHAICONGVIEC VALUES
(1,1,1,2,GETDATE()),
(2,1,2,3,GETDATE()),
(3,2,1,2,GETDATE());

--Nhật ký hoạt động
INSERT INTO NHATKYHOATDONG VALUES
(1,1,N'Tạo công việc',GETDATE()),
(2,2,N'Cập nhật tiến độ',GETDATE()),
(3,3,N'Hoàn thành task',GETDATE());


/* =========================
14. THÔNG BÁO
========================= */

-- Loại thông báo
INSERT INTO LOAITHONGBAO VALUES (1,N'Công việc');
INSERT INTO LOAITHONGBAO VALUES (2,N'Hệ thống');

-- Thông báo
INSERT INTO THONGBAO VALUES 
(1,1,N'Bạn được giao công việc mới',GETDATE()),
(2,2,N'Hệ thống cập nhật KPI',GETDATE());

-- Thông báo nhân viên
INSERT INTO THONGBAO_NHANVIEN VALUES (1,1,0);
INSERT INTO THONGBAO_NHANVIEN VALUES (2,1,1);
INSERT INTO THONGBAO_NHANVIEN VALUES (3,2,0);


/* =========================
15. PHÂN CÔNG KHÁC
========================= */

-- Phân công nhóm
INSERT INTO PHANCONGNHOM VALUES
(1,1,GETDATE()),
(2,2,GETDATE());

-- Phân công phòng ban
INSERT INTO PHANCONGPHONGBAN VALUES
(1,1),
(2,2);


/* =========================
16. TIẾN ĐỘ (CUỐI)
========================= */

INSERT INTO TIENDOCONGVIEC VALUES
('TD1',1,70,2,GETDATE()),
('TD2',2,40,2,GETDATE()),
('TD3',3,90,2,GETDATE()),
('TD4',1,100,3,GETDATE()),
('TD5',2,60,2,GETDATE());