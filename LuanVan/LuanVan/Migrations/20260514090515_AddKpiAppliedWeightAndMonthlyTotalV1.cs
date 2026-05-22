using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LuanVan.Migrations
{
    /// <inheritdoc />
    public partial class AddKpiAppliedWeightAndMonthlyTotalV1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('KPI_PHONGBAN', 'TRONGSO_APDUNG') IS NULL
    ALTER TABLE [KPI_PHONGBAN] ADD [TRONGSO_APDUNG] decimal(5,2) NOT NULL CONSTRAINT [DF_KPI_PHONGBAN_TRONGSO_APDUNG] DEFAULT 0.0;
IF COL_LENGTH('KPI_NHOM', 'TRONGSO_APDUNG') IS NULL
    ALTER TABLE [KPI_NHOM] ADD [TRONGSO_APDUNG] decimal(5,2) NOT NULL CONSTRAINT [DF_KPI_NHOM_TRONGSO_APDUNG] DEFAULT 0.0;
IF COL_LENGTH('KPI_NHANVIEN', 'TRONGSO_APDUNG') IS NULL
    ALTER TABLE [KPI_NHANVIEN] ADD [TRONGSO_APDUNG] decimal(5,2) NOT NULL CONSTRAINT [DF_KPI_NHANVIEN_TRONGSO_APDUNG] DEFAULT 0.0;
IF COL_LENGTH('KPI_DUAN', 'TRONGSO_APDUNG') IS NULL
    ALTER TABLE [KPI_DUAN] ADD [TRONGSO_APDUNG] decimal(5,2) NOT NULL CONSTRAINT [DF_KPI_DUAN_TRONGSO_APDUNG] DEFAULT 0.0;
");

            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[KETQUAKPI_TONG]', N'U') IS NULL
BEGIN
    CREATE TABLE [KETQUAKPI_TONG] (
        [MAKETQUATONG] int NOT NULL IDENTITY(1,1),
        [MANHANVIEN] int NOT NULL,
        [THANG] int NOT NULL,
        [NAM] int NOT NULL,
        [DIEMTONG] decimal(5,2) NOT NULL,
        [XEPLOAI] nvarchar(50) NULL,
        [SOKPI_THANHPHAN] int NOT NULL,
        [NGAYTINH] datetime2(0) NOT NULL CONSTRAINT [DF_KETQUAKPI_TONG_NGAYTINH] DEFAULT (GETDATE()),
        CONSTRAINT [PK_KETQUAKPI_TONG] PRIMARY KEY ([MAKETQUATONG]),
        CONSTRAINT [FK_KETQUAKPI_TONG_NHANVIEN] FOREIGN KEY ([MANHANVIEN]) REFERENCES [NHANVIEN]([MANHANVIEN]) ON DELETE CASCADE
    );
END
");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_KPI_PHONGBAN_TRONGSO_APDUNG' AND parent_object_id = OBJECT_ID(N'[KPI_PHONGBAN]'))
    ALTER TABLE [KPI_PHONGBAN] ADD CONSTRAINT [CK_KPI_PHONGBAN_TRONGSO_APDUNG] CHECK ([TRONGSO_APDUNG] >= 0 AND [TRONGSO_APDUNG] <= 100);
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_KPI_NHOM_TRONGSO_APDUNG' AND parent_object_id = OBJECT_ID(N'[KPI_NHOM]'))
    ALTER TABLE [KPI_NHOM] ADD CONSTRAINT [CK_KPI_NHOM_TRONGSO_APDUNG] CHECK ([TRONGSO_APDUNG] >= 0 AND [TRONGSO_APDUNG] <= 100);
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_KPI_NHANVIEN_TRONGSO_APDUNG' AND parent_object_id = OBJECT_ID(N'[KPI_NHANVIEN]'))
    ALTER TABLE [KPI_NHANVIEN] ADD CONSTRAINT [CK_KPI_NHANVIEN_TRONGSO_APDUNG] CHECK ([TRONGSO_APDUNG] >= 0 AND [TRONGSO_APDUNG] <= 100);
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_KPI_DUAN_TRONGSO_APDUNG' AND parent_object_id = OBJECT_ID(N'[KPI_DUAN]'))
    ALTER TABLE [KPI_DUAN] ADD CONSTRAINT [CK_KPI_DUAN_TRONGSO_APDUNG] CHECK ([TRONGSO_APDUNG] >= 0 AND [TRONGSO_APDUNG] <= 100);
");

            migrationBuilder.Sql(
                """
                IF COL_LENGTH('KPI_NHANVIEN','TRONGSO_APDUNG') IS NOT NULL
                BEGIN
                    ;WITH src AS (
                        SELECT
                            kn.MAKPI,
                            kn.MANHANVIEN,
                            CAST(CASE WHEN dk.TRONGSOGOC IS NULL OR dk.TRONGSOGOC <= 0 THEN 1 ELSE dk.TRONGSOGOC END AS decimal(18,6)) AS TRONGSO_GOC,
                            ROW_NUMBER() OVER (PARTITION BY kn.MANHANVIEN ORDER BY kn.MAKPI) AS RN,
                            COUNT(*) OVER (PARTITION BY kn.MANHANVIEN) AS CNT,
                            SUM(CAST(CASE WHEN dk.TRONGSOGOC IS NULL OR dk.TRONGSOGOC <= 0 THEN 1 ELSE dk.TRONGSOGOC END AS decimal(18,6)))
                                OVER (PARTITION BY kn.MANHANVIEN) AS TONG_GOC
                        FROM KPI_NHANVIEN kn
                        LEFT JOIN DANHMUCKPI dk ON dk.MAKPI = kn.MAKPI
                        WHERE kn.IS_ACTIVE = 1
                    ),
                    rounded AS (
                        SELECT
                            MAKPI,
                            MANHANVIEN,
                            RN,
                            CNT,
                            CASE
                                WHEN TONG_GOC > 0 THEN ROUND((TRONGSO_GOC / TONG_GOC) * 100.0, 2)
                                ELSE ROUND(100.0 / NULLIF(CNT, 0), 2)
                            END AS W
                        FROM src
                    ),
                    calc AS (
                        SELECT
                            MAKPI,
                            MANHANVIEN,
                            CASE
                                WHEN RN = CNT
                                    THEN CAST(100.00 - SUM(CASE WHEN RN < CNT THEN W ELSE 0 END) OVER (PARTITION BY MANHANVIEN) AS decimal(5,2))
                                ELSE CAST(W AS decimal(5,2))
                            END AS TRONGSO_APDUNG
                        FROM rounded
                    )
                    UPDATE kn
                    SET kn.TRONGSO_APDUNG =
                        CASE
                            WHEN c.TRONGSO_APDUNG < 0 THEN 0
                            WHEN c.TRONGSO_APDUNG > 100 THEN 100
                            ELSE c.TRONGSO_APDUNG
                        END
                    FROM KPI_NHANVIEN kn
                    INNER JOIN calc c
                        ON kn.MAKPI = c.MAKPI
                        AND kn.MANHANVIEN = c.MANHANVIEN;
                END
                """);

            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[KETQUAKPI]', N'U') IS NOT NULL
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_KETQUAKPI_MONTH' AND object_id = OBJECT_ID(N'[KETQUAKPI]'))
    CREATE INDEX [IX_KETQUAKPI_MONTH] ON [KETQUAKPI]([MANHANVIEN], [THANG], [NAM]);

IF OBJECT_ID(N'[KETQUAKPI_TONG]', N'U') IS NOT NULL
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UQ_KETQUAKPI_TONG_NV_THANG_NAM' AND object_id = OBJECT_ID(N'[KETQUAKPI_TONG]'))
    CREATE UNIQUE INDEX [UQ_KETQUAKPI_TONG_NV_THANG_NAM] ON [KETQUAKPI_TONG]([MANHANVIEN], [THANG], [NAM]);
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UQ_KETQUAKPI_TONG_NV_THANG_NAM' AND object_id = OBJECT_ID(N'[KETQUAKPI_TONG]'))
    DROP INDEX [UQ_KETQUAKPI_TONG_NV_THANG_NAM] ON [KETQUAKPI_TONG];

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_KETQUAKPI_MONTH' AND object_id = OBJECT_ID(N'[KETQUAKPI]'))
    DROP INDEX [IX_KETQUAKPI_MONTH] ON [KETQUAKPI];

IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_KPI_PHONGBAN_TRONGSO_APDUNG' AND parent_object_id = OBJECT_ID(N'[KPI_PHONGBAN]'))
    ALTER TABLE [KPI_PHONGBAN] DROP CONSTRAINT [CK_KPI_PHONGBAN_TRONGSO_APDUNG];
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_KPI_NHOM_TRONGSO_APDUNG' AND parent_object_id = OBJECT_ID(N'[KPI_NHOM]'))
    ALTER TABLE [KPI_NHOM] DROP CONSTRAINT [CK_KPI_NHOM_TRONGSO_APDUNG];
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_KPI_NHANVIEN_TRONGSO_APDUNG' AND parent_object_id = OBJECT_ID(N'[KPI_NHANVIEN]'))
    ALTER TABLE [KPI_NHANVIEN] DROP CONSTRAINT [CK_KPI_NHANVIEN_TRONGSO_APDUNG];
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_KPI_DUAN_TRONGSO_APDUNG' AND parent_object_id = OBJECT_ID(N'[KPI_DUAN]'))
    ALTER TABLE [KPI_DUAN] DROP CONSTRAINT [CK_KPI_DUAN_TRONGSO_APDUNG];

IF OBJECT_ID(N'[KETQUAKPI_TONG]', N'U') IS NOT NULL
    DROP TABLE [KETQUAKPI_TONG];

IF COL_LENGTH('KPI_PHONGBAN', 'TRONGSO_APDUNG') IS NOT NULL
    ALTER TABLE [KPI_PHONGBAN] DROP COLUMN [TRONGSO_APDUNG];
IF COL_LENGTH('KPI_NHOM', 'TRONGSO_APDUNG') IS NOT NULL
    ALTER TABLE [KPI_NHOM] DROP COLUMN [TRONGSO_APDUNG];
IF COL_LENGTH('KPI_NHANVIEN', 'TRONGSO_APDUNG') IS NOT NULL
    ALTER TABLE [KPI_NHANVIEN] DROP COLUMN [TRONGSO_APDUNG];
IF COL_LENGTH('KPI_DUAN', 'TRONGSO_APDUNG') IS NOT NULL
    ALTER TABLE [KPI_DUAN] DROP COLUMN [TRONGSO_APDUNG];
");
        }
    }
}
