SET QUOTED_IDENTIFIER ON
SET ANSI_NULLS ON

-- Create test employee user
DECLARE @UserId NVARCHAR(128) = 'test-employee-001'
DECLARE @UserName NVARCHAR(256) = 'employee.test'
DECLARE @Email NVARCHAR(256) = 'employee@test.com'

-- Check if user already exists
IF NOT EXISTS (SELECT 1 FROM AspNetUsers WHERE Id = @UserId)
BEGIN
    INSERT INTO AspNetUsers (
        Id, UserName, NormalizedUserName, Email, NormalizedEmail, 
        EmailConfirmed, PasswordHash, SecurityStamp, ConcurrencyStamp, 
        PhoneNumber, PhoneNumberConfirmed, TwoFactorEnabled, 
        LockoutEnd, LockoutEnabled, AccessFailedCount
    )
    VALUES (
        @UserId,
        @UserName,
        UPPER(@UserName),
        @Email,
        UPPER(@Email),
        1,
        'AQAAAAIAAYagAAAAEMxgJ7q7/YYYn4K5p5q7q7q7q7q7q7q7q7q7q7q7q7q7q7q7q7q7q7q7q7q7q7q7q7q7w==',
        'test-security-stamp-001',
        NEWID(),
        NULL,
        0,
        0,
        NULL,
        1,
        0
    )
    
    -- Assign Employee role
    DECLARE @EmployeeRoleId NVARCHAR(128) = (SELECT Id FROM AspNetRoles WHERE Name = 'Employee')
    IF @EmployeeRoleId IS NOT NULL
    BEGIN
        INSERT INTO AspNetUserRoles (UserId, RoleId)
        VALUES (@UserId, @EmployeeRoleId)
    END
    
    -- Create NhanVien record
    DECLARE @PhongBanId INT = (SELECT TOP 1 MAPHONGBAN FROM PHONGBAN ORDER BY MAPHONGBAN)
    DECLARE @ChucVuId INT = (SELECT TOP 1 MACHUCVU FROM CHUCVU ORDER BY MACHUCVU)
    
    IF @PhongBanId IS NOT NULL AND @ChucVuId IS NOT NULL
    BEGIN
        INSERT INTO NHANVIEN (
            HOTEN, GIOITINH, NGAYSINH, EMAIL, SDT, 
            DIACHI, MAPHONGBAN, MACHUCVU, NGAYVAOLAM, TRANGTHAI, 
            AspNetUserId
        )
        VALUES (
            N'Nhân Viên Test',
            N'Nam',
            DATEFROMPARTS(1990, 1, 1),
            @Email,
            '0123456789',
            N'123 Đường Test',
            @PhongBanId,
            @ChucVuId,
            GETDATE(),
            1,
            @UserId
        )
    END
    
    PRINT 'Test employee created successfully'
END
ELSE
BEGIN
    PRINT 'Test employee already exists'
END
