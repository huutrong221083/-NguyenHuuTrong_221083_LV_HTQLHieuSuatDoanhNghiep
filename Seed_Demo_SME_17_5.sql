USE [LV2026]
GO
SET NOCOUNT ON;
GO

BEGIN TRY
    BEGIN TRANSACTION;

    IF NOT EXISTS (SELECT 1 FROM dbo.AspNetUsers WHERE Id = 'u-trong')
        THROW 50001, N'Khong tim thay AspNetUsers.Id = u-trong.', 1;
    IF NOT EXISTS (SELECT 1 FROM dbo.NHANVIEN WHERE MANHANVIEN = 1)
        THROW 50002, N'Khong tim thay NHANVIEN.MANHANVIEN = 1.', 1;

    DECLARE @AdminPasswordHash nvarchar(max) = (SELECT PasswordHash FROM dbo.AspNetUsers WHERE Id = 'u-trong');
    DECLARE @AdminSecurityStamp nvarchar(max) = (SELECT SecurityStamp FROM dbo.AspNetUsers WHERE Id = 'u-trong');

    EXEC sp_msforeachtable 'ALTER TABLE ? NOCHECK CONSTRAINT ALL';

    DECLARE @sql nvarchar(max) = N'';
    ;WITH t AS (
        SELECT QUOTENAME(s.name) AS SName, QUOTENAME(tb.name) AS TName, tb.name AS RawName
        FROM sys.tables tb
        JOIN sys.schemas s ON s.schema_id = tb.schema_id
        WHERE s.name = 'dbo' AND tb.name <> '__EFMigrationsHistory'
    )
    SELECT @sql = STRING_AGG(
        CASE
            WHEN RawName = 'AspNetUsers' THEN N'DELETE FROM ' + SName + N'.' + TName + N' WHERE Id <> ''u-trong'';'
            WHEN RawName = 'NHANVIEN' THEN N'DELETE FROM ' + SName + N'.' + TName + N' WHERE MANHANVIEN <> 1;'
            ELSE N'DELETE FROM ' + SName + N'.' + TName + N';'
        END
    , CHAR(10))
    FROM t;
    EXEC sp_executesql @sql;

    DBCC CHECKIDENT ('dbo.PHONGBAN', RESEED, 0) WITH NO_INFOMSGS;
    DBCC CHECKIDENT ('dbo.NHANVIEN', RESEED, 1) WITH NO_INFOMSGS;
    DBCC CHECKIDENT ('dbo.NHOM', RESEED, 0) WITH NO_INFOMSGS;
    DBCC CHECKIDENT ('dbo.DUAN', RESEED, 0) WITH NO_INFOMSGS;
    DBCC CHECKIDENT ('dbo.CONGVIEC', RESEED, 0) WITH NO_INFOMSGS;
    DBCC CHECKIDENT ('dbo.KYNANG', RESEED, 0) WITH NO_INFOMSGS;
    DBCC CHECKIDENT ('dbo.THONGBAO', RESEED, 0) WITH NO_INFOMSGS;
    DBCC CHECKIDENT ('dbo.KETQUAKPI', RESEED, 0) WITH NO_INFOMSGS;
    DBCC CHECKIDENT ('dbo.KETQUAKPI_TONG', RESEED, 0) WITH NO_INFOMSGS;
    DBCC CHECKIDENT ('dbo.BAOCAO_PORTAL', RESEED, 0) WITH NO_INFOMSGS;
    DBCC CHECKIDENT ('dbo.YEUCAUBAOCAO', RESEED, 0) WITH NO_INFOMSGS;
    DBCC CHECKIDENT ('dbo.DUDOANAI', RESEED, 0) WITH NO_INFOMSGS;

    INSERT INTO dbo.AspNetRoles(Id, Name, NormalizedName, ConcurrencyStamp)
    VALUES
    ('role-admin','Admin','ADMIN','seed-role-admin'),
    ('role-manager','Manager','MANAGER','seed-role-manager'),
    ('role-employee','Employee','EMPLOYEE','seed-role-employee');

    UPDATE dbo.AspNetUsers
    SET UserName='trong.nguyen', NormalizedUserName='TRONG.NGUYEN',
        Email='trong.nguyen@lv2026.local', NormalizedEmail='TRONG.NGUYEN@LV2026.LOCAL',
        EmailConfirmed=1, SecurityStamp=COALESCE(@AdminSecurityStamp, 'seed-security-u-trong'),
        PasswordHash=@AdminPasswordHash
    WHERE Id='u-trong';

    INSERT INTO dbo.AspNetUserRoles(UserId, RoleId) VALUES ('u-trong','role-admin');

    INSERT INTO dbo.AspNetUsers(Id,UserName,NormalizedUserName,Email,NormalizedEmail,EmailConfirmed,PasswordHash,SecurityStamp,ConcurrencyStamp,PhoneNumber,PhoneNumberConfirmed,TwoFactorEnabled,LockoutEnd,LockoutEnabled,AccessFailedCount)
    VALUES
    ('u-tech-01','lan.pham','LAN.PHAM','lan.pham@lv2026.local','LAN.PHAM@LV2026.LOCAL',1,'seed_hash','seed','seed','0900000002',0,0,NULL,1,0),
    ('u-sales-01','quang.le','QUANG.LE','quang.le@lv2026.local','QUANG.LE@LV2026.LOCAL',1,'seed_hash','seed','seed','0900000003',0,0,NULL,1,0),
    ('u-hr-01','thu.nguyen','THU.NGUYEN','thu.nguyen@lv2026.local','THU.NGUYEN@LV2026.LOCAL',1,'seed_hash','seed','seed','0900000004',0,0,NULL,1,0),
    ('u-fin-01','duc.tran','DUC.TRAN','duc.tran@lv2026.local','DUC.TRAN@LV2026.LOCAL',1,'seed_hash','seed','seed','0900000005',0,0,NULL,1,0),
    ('u-techlead-01','anh.vo','ANH.VO','anh.vo@lv2026.local','ANH.VO@LV2026.LOCAL',1,'seed_hash','seed','seed','0900000006',0,0,NULL,1,0),
    ('u-datalead-01','minh.bui','MINH.BUI','minh.bui@lv2026.local','MINH.BUI@LV2026.LOCAL',1,'seed_hash','seed','seed','0900000007',0,0,NULL,1,0),
    ('u-saleslead-01','linh.do','LINH.DO','linh.do@lv2026.local','LINH.DO@LV2026.LOCAL',1,'seed_hash','seed','seed','0900000008',0,0,NULL,1,0),
    ('u-hrlead-01','ha.pham','HA.PHAM','ha.pham@lv2026.local','HA.PHAM@LV2026.LOCAL',1,'seed_hash','seed','seed','0900000009',0,0,NULL,1,0),
    ('u-tech-02','khoi.ngo','KHOI.NGO','khoi.ngo@lv2026.local','KHOI.NGO@LV2026.LOCAL',1,'seed_hash','seed','seed','0900000010',0,0,NULL,1,0),
    ('u-tech-03','yen.tran','YEN.TRAN','yen.tran@lv2026.local','YEN.TRAN@LV2026.LOCAL',1,'seed_hash','seed','seed','0900000011',0,0,NULL,1,0),
    ('u-tech-04','vu.nguyen','VU.NGUYEN','vu.nguyen@lv2026.local','VU.NGUYEN@LV2026.LOCAL',1,'seed_hash','seed','seed','0900000012',0,0,NULL,1,0),
    ('u-tech-05','son.dang','SON.DANG','son.dang@lv2026.local','SON.DANG@LV2026.LOCAL',1,'seed_hash','seed','seed','0900000013',0,0,NULL,1,0),
    ('u-sales-02','nam.ho','NAM.HO','nam.ho@lv2026.local','NAM.HO@LV2026.LOCAL',1,'seed_hash','seed','seed','0900000014',0,0,NULL,1,0),
    ('u-sales-03','trang.ly','TRANG.LY','trang.ly@lv2026.local','TRANG.LY@LV2026.LOCAL',1,'seed_hash','seed','seed','0900000015',0,0,NULL,1,0),
    ('u-sales-04','hieu.pham','HIEU.PHAM','hieu.pham@lv2026.local','HIEU.PHAM@LV2026.LOCAL',1,'seed_hash','seed','seed','0900000016',0,0,NULL,1,0),
    ('u-hr-02','mai.vu','MAI.VU','mai.vu@lv2026.local','MAI.VU@LV2026.LOCAL',1,'seed_hash','seed','seed','0900000017',0,0,NULL,1,0),
    ('u-hr-03','thao.le','THAO.LE','thao.le@lv2026.local','THAO.LE@LV2026.LOCAL',1,'seed_hash','seed','seed','0900000018',0,0,NULL,1,0),
    ('u-fin-02','binh.phan','BINH.PHAN','binh.phan@lv2026.local','BINH.PHAN@LV2026.LOCAL',1,'seed_hash','seed','seed','0900000019',0,0,NULL,1,0),
    ('u-fin-03','nhi.dao','NHI.DAO','nhi.dao@lv2026.local','NHI.DAO@LV2026.LOCAL',1,'seed_hash','seed','seed','0900000020',0,0,NULL,1,0),
    ('u-fin-04','kien.vo','KIEN.VO','kien.vo@lv2026.local','KIEN.VO@LV2026.LOCAL',1,'seed_hash','seed','seed','0900000021',0,0,NULL,1,0);

    INSERT INTO dbo.AspNetUserRoles(UserId, RoleId)
    SELECT Id, CASE WHEN Id IN ('u-tech-01','u-sales-01','u-hr-01','u-fin-01','u-techlead-01','u-datalead-01','u-saleslead-01','u-hrlead-01')
                    THEN 'role-manager' ELSE 'role-employee' END
    FROM dbo.AspNetUsers
    WHERE Id <> 'u-trong';

    INSERT INTO dbo.CHUCVU(TENCHUCVU) VALUES (N'Quản trị hệ thống'),(N'Trưởng phòng'),(N'Trưởng nhóm'),(N'Nhân viên');
    INSERT INTO dbo.DOKHO(TENDOKHO,ISACTIVE,HESO) VALUES (N'Dễ',1,1),(N'Trung bình',1,2),(N'Khó',1,3);
    INSERT INTO dbo.DOUUTIEN(TENDOUUTIEN,ISACTIVE,HESO) VALUES (N'Thấp',1,1),(N'Trung bình',1,2),(N'Cao',1,3),(N'Khẩn cấp',1,4);
    INSERT INTO dbo.TRANGTHAICONGVIEC(TENTRANGTHAI) VALUES (N'Chưa bắt đầu'),(N'Đang thực hiện'),(N'Chờ phê duyệt'),(N'Hoàn thành'),(N'Trễ hạn'),(N'Tạm dừng');
    INSERT INTO dbo.LOAITHONGBAO(TENLOAI) VALUES (N'Hệ thống'),(N'Công việc'),(N'KPI'),(N'AI cảnh báo');

    SET IDENTITY_INSERT dbo.PHONGBAN ON;
    INSERT INTO dbo.PHONGBAN(MAPHONGBAN,TENPHONGBAN,MOTA,MATRUONGPHONG)
    VALUES (1,N'Công nghệ',N'Phát triển sản phẩm và AI',2),(2,N'Kinh doanh / Sales',N'Phát triển doanh thu SME',3),(3,N'Nhân sự',N'Vận hành nhân sự nội bộ',4),(4,N'Tài chính',N'Kiểm soát ngân sách và vận hành',5);
    SET IDENTITY_INSERT dbo.PHONGBAN OFF;

    SET IDENTITY_INSERT dbo.NHANVIEN ON;
    UPDATE dbo.NHANVIEN
    SET MAPHONGBAN=1,HOTEN=N'Nguyễn Hữu Trọng',NGAYSINH='1995-10-10',CCCD='001095000001',DIACHI=N'TP.HCM',GIOITINH=N'Nam',EMAIL='trong.nguyen@lv2026.local',SDT='0900000001',NGAYVAOLAM='2022-01-03',TRANGTHAI=1,AspNetUserId='u-trong',MACHUCVU=1
    WHERE MANHANVIEN=1;
    INSERT INTO dbo.NHANVIEN(MANHANVIEN,MAPHONGBAN,PHO_MAPHONGBAN,HOTEN,NGAYSINH,CCCD,DIACHI,GIOITINH,EMAIL,SDT,NGAYVAOLAM,TRANGTHAI,AspNetUserId,MACHUCVU) VALUES
    (2,1,NULL,N'Phạm Ngọc Lan','1991-01-12','001091000002',N'TP.HCM',N'Nữ','lan.pham@lv2026.local','0900000002','2021-03-01',1,'u-tech-01',2),
    (3,2,NULL,N'Lê Minh Quang','1990-06-21','001090000003',N'TP.HCM',N'Nam','quang.le@lv2026.local','0900000003','2021-03-01',1,'u-sales-01',2),
    (4,3,NULL,N'Nguyễn Hồng Thu','1992-09-09','001092000004',N'TP.HCM',N'Nữ','thu.nguyen@lv2026.local','0900000004','2021-03-01',1,'u-hr-01',2),
    (5,4,NULL,N'Trần Hoàng Đức','1989-11-30','001089000005',N'TP.HCM',N'Nam','duc.tran@lv2026.local','0900000005','2021-03-01',1,'u-fin-01',2),
    (6,1,NULL,N'Võ Tuấn Anh','1993-03-01','001093000006',N'TP.HCM',N'Nam','anh.vo@lv2026.local','0900000006','2022-04-01',1,'u-techlead-01',3),
    (7,1,NULL,N'Bùi Gia Minh','1994-08-12','001094000007',N'TP.HCM',N'Nam','minh.bui@lv2026.local','0900000007','2022-05-10',1,'u-datalead-01',3),
    (8,2,NULL,N'Đỗ Ngọc Linh','1994-04-23','001094000008',N'TP.HCM',N'Nữ','linh.do@lv2026.local','0900000008','2022-05-10',1,'u-saleslead-01',3),
    (9,3,NULL,N'Phạm Thanh Hà','1995-07-19','001095000009',N'TP.HCM',N'Nữ','ha.pham@lv2026.local','0900000009','2022-05-10',1,'u-hrlead-01',3),
    (10,1,NULL,N'Ngô Trung Khôi','1998-02-14','001098000010',N'TP.HCM',N'Nam','khoi.ngo@lv2026.local','0900000010','2023-02-01',1,'u-tech-02',4),
    (11,1,NULL,N'Trần Hải Yến','1997-12-03','001097000011',N'TP.HCM',N'Nữ','yen.tran@lv2026.local','0900000011','2023-02-01',1,'u-tech-03',4),
    (12,1,NULL,N'Nguyễn Hữu Vũ','1998-06-18','001098000012',N'TP.HCM',N'Nam','vu.nguyen@lv2026.local','0900000012','2023-02-01',1,'u-tech-04',4),
    (13,1,NULL,N'Đặng Quốc Sơn','1999-10-20','001099000013',N'TP.HCM',N'Nam','son.dang@lv2026.local','0900000013','2023-02-01',1,'u-tech-05',4),
    (14,2,NULL,N'Hồ Việt Nam','1997-05-07','001097000014',N'TP.HCM',N'Nam','nam.ho@lv2026.local','0900000014','2023-02-01',1,'u-sales-02',4),
    (15,2,NULL,N'Lý Thu Trang','1998-01-24','001098000015',N'TP.HCM',N'Nữ','trang.ly@lv2026.local','0900000015','2023-02-01',1,'u-sales-03',4),
    (16,2,NULL,N'Phạm Quốc Hiếu','1996-09-17','001096000016',N'TP.HCM',N'Nam','hieu.pham@lv2026.local','0900000016','2023-02-01',1,'u-sales-04',4),
    (17,3,NULL,N'Vũ Thanh Mai','1998-11-11','001098000017',N'TP.HCM',N'Nữ','mai.vu@lv2026.local','0900000017','2023-02-01',1,'u-hr-02',4),
    (18,3,NULL,N'Lê Minh Thảo','1999-03-28','001099000018',N'TP.HCM',N'Nữ','thao.le@lv2026.local','0900000018','2023-02-01',1,'u-hr-03',4),
    (19,4,NULL,N'Phan Đức Bình','1996-08-06','001096000019',N'TP.HCM',N'Nam','binh.phan@lv2026.local','0900000019','2023-02-01',1,'u-fin-02',4),
    (20,4,NULL,N'Đào Bảo Nhi','1997-01-15','001097000020',N'TP.HCM',N'Nữ','nhi.dao@lv2026.local','0900000020','2023-02-01',1,'u-fin-03',4),
    (21,4,NULL,N'Võ Gia Kiên','1998-04-26','001098000021',N'TP.HCM',N'Nam','kien.vo@lv2026.local','0900000021','2023-02-01',1,'u-fin-04',4);
    SET IDENTITY_INSERT dbo.NHANVIEN OFF;

    SET IDENTITY_INSERT dbo.NHOM ON;
    INSERT INTO dbo.NHOM(MANHOM,TENNHOM,NGAYTAO,TRUONGNHOM)
    VALUES (1,N'Web Portal','2026-01-05',6),(2,N'AI/Data','2026-01-05',7),(3,N'Sales SME','2026-01-05',8),(4,N'Nhân sự nội bộ','2026-01-05',9),(5,N'Tài chính vận hành','2026-01-05',5);
    SET IDENTITY_INSERT dbo.NHOM OFF;

    SET IDENTITY_INSERT dbo.KYNANG ON;
    INSERT INTO dbo.KYNANG(MAKYNANG,TENKYNANG,MOTA,TRANGTHAI) VALUES
    (1,N'ASP.NET Core',N'Phát triển backend web',1),(2,N'SQL Server',N'Thiết kế và truy vấn cơ sở dữ liệu',1),(3,N'JavaScript',N'Xử lý giao diện và tương tác',1),
    (4,N'UI/UX',N'Thiết kế giao diện người dùng',1),(5,N'Kiểm thử phần mềm',N'Kiểm thử chức năng và tích hợp',1),(6,N'Phân tích dữ liệu',N'Xử lý dữ liệu và báo cáo',1),
    (7,N'Machine Learning',N'Xây dựng mô hình AI cơ bản',1),(8,N'Chăm sóc khách hàng',N'Theo dõi và tư vấn khách hàng',1),(9,N'Tư vấn bán hàng',N'Chốt cơ hội kinh doanh',1),
    (10,N'Tuyển dụng',N'Quản lý hồ sơ và quy trình tuyển',1),(11,N'Báo cáo tài chính',N'Tổng hợp và phân tích chi phí',1),(12,N'Quản lý dự án',N'Lập kế hoạch và điều phối công việc',1);
    SET IDENTITY_INSERT dbo.KYNANG OFF;

    INSERT INTO dbo.THANHVIENNHOM(MANHANVIEN,MANHOM,NGAYGIANHAP,VAITROTRONGNHOM)
    VALUES
    (6,1,'2026-01-05',N'Trưởng nhóm'),(10,1,'2026-01-05',N'Frontend'),(11,1,'2026-01-05',N'Backend'),(12,1,'2026-01-05',N'QA'),
    (7,2,'2026-01-05',N'Trưởng nhóm'),(13,2,'2026-01-05',N'Data Engineer'),(1,2,'2026-01-05',N'Kiến trúc AI'),
    (8,3,'2026-01-05',N'Trưởng nhóm'),(14,3,'2026-01-05',N'Sales Executive'),(15,3,'2026-01-05',N'Sales Executive'),(16,3,'2026-01-05',N'Sales Executive'),
    (9,4,'2026-01-05',N'Trưởng nhóm'),(17,4,'2026-01-05',N'HRBP'),(18,4,'2026-01-05',N'HR Ops'),
    (5,5,'2026-01-05',N'Trưởng nhóm'),(19,5,'2026-01-05',N'Kế toán'),(20,5,'2026-01-05',N'Kế toán'),(21,5,'2026-01-05',N'Phân tích tài chính');

    INSERT INTO dbo.KYNANGNHANVIEN(MAKYNANG,MANHANVIEN,CAPDO,SODUANDADUNG)
    VALUES
    (12,1,5,10),(1,6,5,8),(2,6,4,8),(12,6,4,10),(7,7,5,7),(6,7,4,7),(9,8,5,9),(8,8,4,8),(10,9,5,8),
    (1,10,4,5),(3,10,4,5),(2,11,4,5),(5,12,4,6),(6,13,4,4),(9,14,4,6),(9,15,3,5),(9,16,3,4),(10,17,4,5),(10,18,4,4),(11,19,4,6),(11,20,4,6),(11,21,4,6);

    INSERT INTO dbo.DUAN(TENDUAN,MOTA,NGAYBATDAU,NGAYKETTHUC,TRANGTHAI)
    VALUES
    (N'Portal Quản trị SME',N'Nâng cấp cổng quản trị tổng thể','2026-01-10','2026-05-17',2),
    (N'AI Dự báo hiệu suất',N'Mô hình dự báo và cảnh báo công việc','2026-02-01','2026-05-17',2),
    (N'Tối ưu doanh số Sales',N'Chuẩn hóa pipeline sales và KPI','2026-01-15','2026-05-17',2),
    (N'Chuẩn hóa vận hành nội bộ',N'Số hóa quy trình HR + Finance','2026-01-20','2026-05-17',2);

    INSERT INTO dbo.DUAN_PHONGBAN(MADUAN,MAPHONGBAN,NGAYTHAMGIA,TRANGTHAI)
    VALUES (1,1,'2026-01-10',1),(2,1,'2026-02-01',1),(3,2,'2026-01-15',1),(4,3,'2026-01-20',1),(4,4,'2026-01-20',1);
    INSERT INTO dbo.DUAN_NHOM(MADUAN,MANHOM,NGAYTHAMGIA,TRANGTHAI)
    VALUES (1,1,'2026-01-10',1),(2,2,'2026-02-01',1),(3,3,'2026-01-15',1),(4,4,'2026-01-20',1),(4,5,'2026-01-20',1);
    INSERT INTO dbo.DUAN_NHANVIEN(MADUAN,MANHANVIEN,VAITRO,NGAYTHAMGIA,NGAYROI,TRANGTHAI)
    VALUES
    (1,2,N'PM','2026-01-10',NULL,1),(1,6,N'Lead','2026-01-10',NULL,1),(1,10,N'Dev','2026-01-10',NULL,1),(1,11,N'Dev','2026-01-10',NULL,1),(1,12,N'QA','2026-01-10',NULL,1),
    (2,1,N'Giám sát AI','2026-02-01',NULL,1),(2,7,N'Lead AI','2026-02-01',NULL,1),(2,13,N'Data Engineer','2026-02-01',NULL,1),
    (3,3,N'Manager Sales','2026-01-15',NULL,1),(3,8,N'Lead Sales','2026-01-15',NULL,1),(3,14,N'Sales','2026-01-15',NULL,1),(3,15,N'Sales','2026-01-15',NULL,1),(3,16,N'Sales','2026-01-15',NULL,1),
    (4,4,N'HR Manager','2026-01-20',NULL,1),(4,9,N'HR Lead','2026-01-20',NULL,1),(4,17,N'HRBP','2026-01-20',NULL,1),(4,18,N'HR Ops','2026-01-20',NULL,1),
    (4,5,N'Finance Manager','2026-01-20',NULL,1),(4,19,N'Accountant','2026-01-20',NULL,1),(4,20,N'Accountant','2026-01-20',NULL,1),(4,21,N'Analyst','2026-01-20',NULL,1);

    INSERT INTO dbo.CONGVIEC(MADOUUTIEN,MADOKHO,MATRANGTHAI,MADUAN,MACONGVIECCHA,TENCONGVIEC,MOTA,HANHOANTHANH,DIEMCONGVIEC,DIEMPHUCTAP,NGAYBATDAU,PHANTRAMHOANTHANH,NGAYTAO,NGUOITAO,NGAYCAPNHAT,NGUOICAPNHAT,DAXOA)
    VALUES
    (3,2,4,1,NULL,N'Chốt sitemap portal',N'Hoàn tất luồng chính','2026-02-10',90,NULL,'2026-01-12',100,'2026-01-12','u-tech-01','2026-02-10','u-tech-01',0),
    (3,3,4,1,NULL,N'RBAC phân quyền',N'Phân quyền theo phòng ban','2026-03-15',88,NULL,'2026-02-01',100,'2026-02-01','u-tech-01','2026-03-15','u-tech-01',0),
    (2,2,3,1,NULL,N'Kiểm thử tích hợp',N'Kiểm thử end-to-end','2026-04-20',78,NULL,'2026-03-20',85,'2026-03-20','u-techlead-01','2026-04-18','u-techlead-01',0),
    (4,3,5,1,NULL,N'Fix timeout dashboard',N'Khắc phục timeout','2026-04-10',60,NULL,'2026-03-28',50,'2026-03-28','u-tech-03','2026-04-15','u-tech-03',0),
    (2,1,2,1,NULL,N'Responsive mobile task',N'Tối ưu mobile','2026-05-17',72,NULL,'2026-05-01',68,'2026-05-01','u-tech-02','2026-05-16','u-tech-02',0),
    (3,2,4,1,NULL,N'Tối ưu truy vấn KPI',N'Cải thiện tốc độ query','2026-05-12',84,NULL,'2026-04-25',100,'2026-04-25','u-tech-04','2026-05-12','u-tech-04',0),
    (3,2,2,1,NULL,N'Triển khai dashboard KPI',N'Đồng bộ dữ liệu KPI','2026-05-17',76,NULL,'2026-05-03',60,'2026-05-03','u-tech-01','2026-05-17','u-tech-01',0),
    (3,2,4,2,NULL,N'Làm sạch dữ liệu train',N'Tổng hợp dữ liệu 6 tháng','2026-03-10',86,NULL,'2026-02-05',100,'2026-02-05','u-datalead-01','2026-03-10','u-datalead-01',0),
    (4,3,4,2,NULL,N'Huấn luyện model v1',N'Classifier đầu tiên','2026-04-15',82,NULL,'2026-03-01',100,'2026-03-01','u-datalead-01','2026-04-15','u-datalead-01',0),
    (3,2,3,2,NULL,N'Đánh giá model tháng 4',N'Precision/Recall','2026-04-30',79,NULL,'2026-04-20',88,'2026-04-20','u-trong','2026-04-30','u-trong',0),
    (2,2,2,2,NULL,N'Xây API cảnh báo AI',N'API cho dashboard','2026-05-17',74,NULL,'2026-05-04',62,'2026-05-04','u-tech-04','2026-05-16','u-tech-04',0),
    (4,3,5,2,NULL,N'Backfill đặc trưng',N'Bổ sung log lịch sử','2026-05-05',58,NULL,'2026-04-18',45,'2026-04-18','u-tech-05','2026-05-09','u-tech-05',0),
    (3,2,3,2,NULL,N'Duyệt ngưỡng cảnh báo',N'Ngưỡng trễ hạn mới','2026-05-16',80,NULL,'2026-05-08',86,'2026-05-08','u-trong','2026-05-16','u-trong',0),
    (3,2,4,3,NULL,N'Chuẩn hóa lead list',N'Làm sạch lead','2026-02-28',92,NULL,'2026-01-20',100,'2026-01-20','u-saleslead-01','2026-02-28','u-saleslead-01',0),
    (4,3,4,3,NULL,N'Playbook gọi điện',N'Kịch bản theo phân khúc','2026-03-30',90,NULL,'2026-02-15',100,'2026-02-15','u-saleslead-01','2026-03-30','u-saleslead-01',0),
    (3,2,4,3,NULL,N'Rà soát hợp đồng',N'Chuẩn bị ký doanh thu','2026-04-25',87,NULL,'2026-04-01',100,'2026-04-01','u-sales-02','2026-04-25','u-sales-02',0),
    (4,3,5,3,NULL,N'Chốt deal khách A',N'Trễ vì đổi điều khoản','2026-05-02',55,NULL,'2026-04-12',50,'2026-04-12','u-sales-04','2026-05-07','u-sales-04',0),
    (2,2,2,3,NULL,N'Upsell tệp khách cũ',N'Chiến dịch tháng 5','2026-05-17',78,NULL,'2026-05-02',70,'2026-05-02','u-saleslead-01','2026-05-17','u-saleslead-01',0),
    (2,1,3,3,NULL,N'Workshop khách hàng',N'Buổi tư vấn trực tuyến','2026-05-15',73,NULL,'2026-05-05',82,'2026-05-05','u-sales-03','2026-05-15','u-sales-03',0),
    (2,1,4,4,NULL,N'Chuẩn hóa onboarding',N'Ban hành quy trình mới','2026-03-05',88,NULL,'2026-02-01',100,'2026-02-01','u-hrlead-01','2026-03-05','u-hrlead-01',0),
    (3,2,4,4,NULL,N'Số hóa hồ sơ HR',N'Scan và metadata','2026-04-22',86,NULL,'2026-03-05',100,'2026-03-05','u-hr-02','2026-04-22','u-hr-02',0),
    (2,1,3,4,NULL,N'Phê duyệt chính sách phép',N'Chờ ký duyệt','2026-05-14',77,NULL,'2026-04-20',84,'2026-04-20','u-hr-03','2026-05-14','u-hr-01',0),
    (2,1,2,4,NULL,N'Kế hoạch tuyển dụng Q3',N'Tổng hợp nhu cầu','2026-05-17',74,NULL,'2026-05-01',62,'2026-05-01','u-hr-01','2026-05-17','u-hr-01',0),
    (2,1,4,4,NULL,N'Khảo sát hài lòng nhân viên',N'Khảo sát tháng 4','2026-04-27',85,NULL,'2026-04-01',100,'2026-04-01','u-hr-01','2026-04-27','u-hr-01',0),
    (2,1,4,4,NULL,N'Đối soát công nợ',N'Đúng hạn tháng 3','2026-03-31',90,NULL,'2026-03-01',100,'2026-03-01','u-fin-02','2026-03-31','u-fin-01',0),
    (3,2,4,4,NULL,N'Báo cáo dòng tiền',N'Báo cáo tháng 4','2026-04-30',89,NULL,'2026-04-01',100,'2026-04-01','u-fin-03','2026-04-30','u-fin-01',0),
    (3,2,2,4,NULL,N'Dự báo ngân sách Q3',N'Tổng hợp kịch bản','2026-05-17',80,NULL,'2026-05-01',68,'2026-05-01','u-fin-04','2026-05-16','u-fin-01',0);

    INSERT INTO dbo.CONGVIEC_KYNANG(MACONGVIEC,MAKYNANG,CAPDO_YEUCAU,TRONGSO)
    SELECT MACONGVIEC, CASE WHEN MACONGVIEC<=7 THEN 1 WHEN MACONGVIEC<=13 THEN 7 WHEN MACONGVIEC<=19 THEN 9 WHEN MACONGVIEC<=24 THEN 10 ELSE 11 END, 3, 25
    FROM dbo.CONGVIEC;

    INSERT INTO dbo.PHANCONGPHONGBAN(MAPHONGBAN,MACONGVIEC,NGAYPHANCONG,TRANGTHAI)
    SELECT CASE WHEN MADUAN IN (1,2) THEN 1 WHEN MADUAN=3 THEN 2 ELSE CASE WHEN MACONGVIEC<=24 THEN 3 ELSE 4 END END, MACONGVIEC, NGAYTAO, 1 FROM dbo.CONGVIEC;
    INSERT INTO dbo.PHANCONGNHOM(MACONGVIEC,MANHOM,NGAYGIAO,TRANGTHAI)
    SELECT MACONGVIEC, CASE WHEN MADUAN=1 THEN 1 WHEN MADUAN=2 THEN 2 WHEN MADUAN=3 THEN 3 WHEN MACONGVIEC<=24 THEN 4 ELSE 5 END, NGAYTAO, 1 FROM dbo.CONGVIEC;

    INSERT INTO dbo.PHANCONGNHANVIEN(MACONGVIEC,MANHANVIEN,NGAYGIAO,NGAYBATDAUDUKIEN,NGAYKETTHUCDUKIEN,NGAYBATDAUTHUCTE,NGAYKETTHUCTHUCTE,TRANGTHAI,PHANTRAMHOANTHANH)
    SELECT c.MACONGVIEC,
           CASE WHEN c.MADUAN=1 THEN CASE WHEN c.MACONGVIEC IN (1,2,7) THEN 6 WHEN c.MACONGVIEC=3 THEN 12 WHEN c.MACONGVIEC=4 THEN 11 WHEN c.MACONGVIEC=5 THEN 10 ELSE 13 END
                WHEN c.MADUAN=2 THEN CASE WHEN c.MACONGVIEC IN (8,9,13) THEN 7 WHEN c.MACONGVIEC IN (10,12) THEN 13 ELSE 12 END
                WHEN c.MADUAN=3 THEN CASE WHEN c.MACONGVIEC IN (14,15,18) THEN 14 WHEN c.MACONGVIEC=16 THEN 8 WHEN c.MACONGVIEC=17 THEN 16 ELSE 15 END
                ELSE CASE WHEN c.MACONGVIEC IN (20,23) THEN 17 WHEN c.MACONGVIEC IN (21,22) THEN 18 WHEN c.MACONGVIEC=24 THEN 19 WHEN c.MACONGVIEC=25 THEN 20 ELSE 21 END END,
           c.NGAYTAO,c.NGAYBATDAU,c.HANHOANTHANH,c.NGAYBATDAU,
           CASE WHEN c.MATRANGTHAI=4 THEN c.HANHOANTHANH WHEN c.MATRANGTHAI=5 THEN DATEADD(day,3,c.HANHOANTHANH) ELSE NULL END,
           c.MATRANGTHAI,COALESCE(c.PHANTRAMHOANTHANH,0)
    FROM dbo.CONGVIEC c;

    INSERT INTO dbo.TIENDOCONGVIEC(MACONGVIEC,PHANTRAMHOANTHANH,TRANGTHAIHIENTAI,NGAYCAPNHAT,TRANGTHAIPHEDUYET,NGUOIPHEDUYET,NGAYPHEDUYET,LYDOTUCHOI)
    SELECT MACONGVIEC, PHANTRAMHOANTHANH, MATRANGTHAI, COALESCE(NGAYCAPNHAT,NGAYTAO),
           CASE WHEN MATRANGTHAI IN (3,4) THEN N'Đã phê duyệt' WHEN MATRANGTHAI=5 THEN N'Yêu cầu cập nhật' ELSE N'Chờ duyệt' END,
           CASE WHEN MATRANGTHAI IN (3,4) THEN 1 ELSE 2 END,
           CASE WHEN MATRANGTHAI IN (3,4) THEN COALESCE(NGAYCAPNHAT,NGAYTAO) ELSE NULL END,
           CASE WHEN MATRANGTHAI=5 THEN N'Vượt hạn cam kết' ELSE NULL END
    FROM dbo.CONGVIEC;

    INSERT INTO dbo.LOAIKPI(TENLOAIKPI,ISACTIVE,HESO)
    VALUES (N'KPI Doanh số',1,1.20),(N'KPI Tiến độ',1,1.00),(N'KPI Chất lượng',1,1.10),(N'KPI Tuân thủ',1,0.90);

    INSERT INTO dbo.KETQUAKPI(MANHANVIEN,MAKPI,DIEMSO,THANG,NAM) VALUES
    (1,1,96,3,2026),(1,1,97,4,2026),(1,1,98,5,2026),
    (2,1,84,3,2026),(2,1,88,4,2026),(2,1,90,5,2026),
    (3,1,102,3,2026),(3,1,110,4,2026),(3,1,100,5,2026),
    (4,1,92,3,2026),(4,1,93,4,2026),(4,1,91,5,2026),
    (5,1,91,3,2026),(5,1,92,4,2026),(5,1,90,5,2026),
    (6,1,86,3,2026),(6,1,89,4,2026),(6,1,87,5,2026),
    (7,1,83,3,2026),(7,1,86,4,2026),(7,1,84,5,2026),
    (8,1,90,3,2026),(8,1,98,4,2026),(8,1,95,5,2026),
    (9,1,88,3,2026),(9,1,89,4,2026),(9,1,88,5,2026),
    (10,1,77,3,2026),(10,1,79,4,2026),(10,1,78,5,2026),
    (11,1,76,3,2026),(11,1,77,4,2026),(11,1,76,5,2026),
    (12,1,72,3,2026),(12,1,74,4,2026),(12,1,73,5,2026),
    (13,1,70,3,2026),(13,1,72,4,2026),(13,1,71,5,2026),
    (14,1,88,3,2026),(14,1,92,4,2026),(14,1,95,5,2026),
    (15,1,79,3,2026),(15,1,82,4,2026),(15,1,82,5,2026),
    (16,1,65,3,2026),(16,1,61,4,2026),(16,1,58,5,2026),
    (17,1,73,3,2026),(17,1,75,4,2026),(17,1,75,5,2026),
    (18,1,74,3,2026),(18,1,76,4,2026),(18,1,77,5,2026),
    (19,1,87,3,2026),(19,1,89,4,2026),(19,1,90,5,2026),
    (20,1,85,3,2026),(20,1,87,4,2026),(20,1,86,5,2026),
    (21,1,69,3,2026),(21,1,70,4,2026),(21,1,72,5,2026);

    INSERT INTO dbo.KETQUAKPI_TONG(MANHANVIEN,THANG,NAM,DIEMTONG,XEPLOAI,SOKPI_THANHPHAN,NGAYTINH) VALUES
    (1,5,2026,98,N'Xuất sắc',3,'2026-05-17'),(2,5,2026,90,N'Tốt',3,'2026-05-17'),(3,5,2026,120,N'Xuất sắc',4,'2026-05-17'),
    (4,5,2026,91,N'Tốt',1,'2026-05-17'),(5,5,2026,90,N'Tốt',1,'2026-05-17'),(6,5,2026,87,N'Tốt',3,'2026-05-17'),
    (7,5,2026,84,N'Khá',3,'2026-05-17'),(8,5,2026,95,N'Xuất sắc',4,'2026-05-17'),(9,5,2026,88,N'Tốt',1,'2026-05-17'),
    (10,5,2026,78,N'Khá',3,'2026-05-17'),(11,5,2026,76,N'Khá',3,'2026-05-17'),(12,5,2026,73,N'Trung bình',3,'2026-05-17'),
    (13,5,2026,71,N'Trung bình',3,'2026-05-17'),(14,5,2026,95,N'Xuất sắc',4,'2026-05-17'),(15,5,2026,82,N'Khá',4,'2026-05-17'),
    (16,5,2026,58,N'Cần cải thiện',4,'2026-05-17'),(17,5,2026,75,N'Khá',1,'2026-05-17'),(18,5,2026,77,N'Khá',1,'2026-05-17'),
    (19,5,2026,90,N'Tốt',1,'2026-05-17'),(20,5,2026,86,N'Tốt',1,'2026-05-17'),(21,5,2026,72,N'Trung bình',1,'2026-05-17');
    SET IDENTITY_INSERT dbo.THONGBAO ON;
    INSERT INTO dbo.THONGBAO(MATHONGBAO,MALOAI,NOIDUNG,THOIGIAN) VALUES
    (1,3,N'KPI Sales tháng 5 ghi nhận 1 nhân sự vượt 120 điểm và 1 nhân sự dưới 60 điểm.','2026-05-17'),
    (2,4,N'AI cảnh báo: Phạm Quốc Hiếu có xác suất trễ hạn cao nhất.','2026-05-17'),
    (3,2,N'Có 4 công việc đang ở trạng thái chờ phê duyệt.','2026-05-17'),
    (4,1,N'Dữ liệu demo SME đã được làm mới thành công.','2026-05-17'),
    (5,3,N'Đề xuất KPI mới cho Sales đang chờ duyệt.','2026-05-17'),
    (6,2,N'Công việc chốt deal khách A đang trễ hạn 3 ngày.','2026-05-17'),
    (7,4,N'AI khuyến nghị giảm tải việc song song cho Phạm Quốc Hiếu.','2026-05-17'),
    (8,2,N'Kế hoạch tuyển dụng Q3 hiện đạt 62% tiến độ.','2026-05-17'),
    (9,3,N'Tổng hợp KPI Công nghệ đã sẵn sàng để duyệt.','2026-05-17'),
    (10,1,N'Đã cập nhật ngưỡng cảnh báo AI cho tháng 5.','2026-05-17'),
    (11,4,N'AI phát hiện xu hướng giảm hiệu suất ở một phần nhóm Sales.','2026-05-17'),
    (12,2,N'Có 2 công việc tạm dừng cần xác nhận timeline.','2026-05-17'),
    (13,3,N'KPI Tài chính duy trì ổn định trên 85 điểm.','2026-05-17');
    SET IDENTITY_INSERT dbo.THONGBAO OFF;

    IF EXISTS (SELECT 1 FROM dbo.THONGBAO_NHANVIEN tv LEFT JOIN dbo.THONGBAO t ON t.MATHONGBAO = tv.MATHONGBAO WHERE t.MATHONGBAO IS NULL)
        THROW 50010, N'Thong bao nhan vien dang tham chieu MATHONGBAO khong ton tai.', 1;
    IF EXISTS (SELECT 1 FROM dbo.KYNANGNHANVIEN knv LEFT JOIN dbo.KYNANG k ON k.MAKYNANG = knv.MAKYNANG WHERE k.MAKYNANG IS NULL)
        THROW 50011, N'Ky nang nhan vien dang tham chieu MAKYNANG khong ton tai.', 1;

    EXEC sp_msforeachtable 'ALTER TABLE ? WITH CHECK CHECK CONSTRAINT ALL';

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    DECLARE @Err nvarchar(4000)=ERROR_MESSAGE();
    RAISERROR(@Err,16,1);
END CATCH;
GO

SELECT N'AspNetUsers' AS Bang, COUNT(*) AS SoLuong FROM dbo.AspNetUsers
UNION ALL SELECT N'NHANVIEN', COUNT(*) FROM dbo.NHANVIEN
UNION ALL SELECT N'PHONGBAN', COUNT(*) FROM dbo.PHONGBAN
UNION ALL SELECT N'NHOM', COUNT(*) FROM dbo.NHOM
UNION ALL SELECT N'KYNANG', COUNT(*) FROM dbo.KYNANG
UNION ALL SELECT N'THONGBAO', COUNT(*) FROM dbo.THONGBAO;
GO
