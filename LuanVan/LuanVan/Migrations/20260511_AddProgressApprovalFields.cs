using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LuanVan.Migrations
{
    /// <inheritdoc />
    public partial class AddProgressApprovalFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LYDOTUCHOI",
                table: "TIENDOCONGVIEC",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "NGAYPHEDUYET",
                table: "TIENDOCONGVIEC",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "NGUOIPHEDUYET",
                table: "TIENDOCONGVIEC",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TRANGTHAIPHEDUYET",
                table: "TIENDOCONGVIEC",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true,
                defaultValue: "Chờ duyệt");

            migrationBuilder.CreateIndex(
                name: "IX_TIENDOCONGVIEC_NGUOIPHEDUYET",
                table: "TIENDOCONGVIEC",
                column: "NGUOIPHEDUYET");

            migrationBuilder.AddForeignKey(
                name: "FK_TIENDOCONGVIEC_NHANVIEN",
                table: "TIENDOCONGVIEC",
                column: "NGUOIPHEDUYET",
                principalTable: "NHANVIEN",
                principalColumn: "MANHANVIEN",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TIENDOCONGVIEC_NHANVIEN",
                table: "TIENDOCONGVIEC");

            migrationBuilder.DropIndex(
                name: "IX_TIENDOCONGVIEC_NGUOIPHEDUYET",
                table: "TIENDOCONGVIEC");

            migrationBuilder.DropColumn(
                name: "LYDOTUCHOI",
                table: "TIENDOCONGVIEC");

            migrationBuilder.DropColumn(
                name: "NGAYPHEDUYET",
                table: "TIENDOCONGVIEC");

            migrationBuilder.DropColumn(
                name: "NGUOIPHEDUYET",
                table: "TIENDOCONGVIEC");

            migrationBuilder.DropColumn(
                name: "TRANGTHAIPHEDUYET",
                table: "TIENDOCONGVIEC");
        }
    }
}
