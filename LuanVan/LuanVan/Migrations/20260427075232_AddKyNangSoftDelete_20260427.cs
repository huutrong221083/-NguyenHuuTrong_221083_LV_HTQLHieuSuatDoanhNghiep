using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LuanVan.Migrations
{
    /// <inheritdoc />
    public partial class AddKyNangSoftDelete_20260427 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Check if column already exists before adding
            migrationBuilder.Sql(
                @"IF NOT EXISTS(SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'KYNANG' AND COLUMN_NAME = 'TRANGTHAI')
                  ALTER TABLE [KYNANG] ADD [TRANGTHAI] int NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TRANGTHAI",
                table: "KYNANG");
        }
    }
}
