using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LuanVan.Migrations
{
    /// <inheritdoc />
    public partial class AddCongViecMissingColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add missing columns to CONGVIEC table
            migrationBuilder.AddColumn<DateTime>(
                name: "NGAYBATDAU",
                table: "CONGVIEC",
                type: "datetime2(0)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PHANTRAMHOANTHANH",
                table: "CONGVIEC",
                type: "decimal(5,2)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "NGAYTAO",
                table: "CONGVIEC",
                type: "datetime2(0)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NGUOITAO",
                table: "CONGVIEC",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "NGAYCAPNHAT",
                table: "CONGVIEC",
                type: "datetime2(0)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NGUOICAPNHAT",
                table: "CONGVIEC",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "DAXOA",
                table: "CONGVIEC",
                type: "bit",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NGAYBATDAU",
                table: "CONGVIEC");

            migrationBuilder.DropColumn(
                name: "PHANTRAMHOANTHANH",
                table: "CONGVIEC");

            migrationBuilder.DropColumn(
                name: "NGAYTAO",
                table: "CONGVIEC");

            migrationBuilder.DropColumn(
                name: "NGUOITAO",
                table: "CONGVIEC");

            migrationBuilder.DropColumn(
                name: "NGAYCAPNHAT",
                table: "CONGVIEC");

            migrationBuilder.DropColumn(
                name: "NGUOICAPNHAT",
                table: "CONGVIEC");

            migrationBuilder.DropColumn(
                name: "DAXOA",
                table: "CONGVIEC");
        }
    }
}
