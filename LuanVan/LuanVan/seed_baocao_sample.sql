-- Insert sample BaoCao data
INSERT INTO [BAOCAO] (
    [TENBAOCAO], 
    [LOAIBAOCAO], 
    [NGUOITAO], 
    [NGAYTAO], 
    [NGAYBATDAU], 
    [NGAYKETTHUC], 
    [DINH_DANG], 
    [TRANGTHAI], 
    [ISDELETED]
) 
SELECT TOP 1
    N'Báo cáo KPI Tháng 5/2026 - ' + u.[UserName],
    'personal',
    u.[Id],
    GETUTCDATE(),
    DATEFROMPARTS(2026, 5, 1),
    DATEFROMPARTS(2026, 5, 31),
    'PDF',
    'completed',
    0
FROM [AspNetUsers] u
WHERE u.[UserName] IS NOT NULL;

INSERT INTO [BAOCAO] (
    [TENBAOCAO], 
    [LOAIBAOCAO], 
    [MAPHONGBAN],
    [NGUOITAO], 
    [NGAYTAO], 
    [NGAYBATDAU], 
    [NGAYKETTHUC], 
    [DINH_DANG], 
    [TRANGTHAI], 
    [ISDELETED]
) 
SELECT TOP 1
    N'Báo cáo Hiệu suất Phòng Ban - ' + pb.[TENPHONGBAN],
    'department',
    pb.[MAPHONGBAN],
    u.[Id],
    GETUTCDATE(),
    DATEFROMPARTS(2026, 4, 1),
    DATEFROMPARTS(2026, 4, 30),
    'Excel',
    'completed',
    0
FROM [PHONGBAN] pb, [AspNetUsers] u
WHERE pb.[MAPHONGBAN] IS NOT NULL AND u.[UserName] IS NOT NULL;

-- Verify inserts
SELECT COUNT(*) as 'Total BaoCao Records' FROM [BAOCAO];
SELECT * FROM [BAOCAO] ORDER BY [NGAYTAO] DESC;
