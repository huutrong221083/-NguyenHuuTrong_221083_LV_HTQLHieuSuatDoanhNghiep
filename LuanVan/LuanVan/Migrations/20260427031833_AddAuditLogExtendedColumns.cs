using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LuanVan.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditLogExtendedColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DOITUONG",
                table: "NHATKYHOATDONG",
                type: "varchar(100)",
                unicode: false,
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DULIEUCU",
                table: "NHATKYHOATDONG",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DULIEUMOI",
                table: "NHATKYHOATDONG",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IP",
                table: "NHATKYHOATDONG",
                type: "varchar(64)",
                unicode: false,
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TRANGTHAI",
                table: "NHATKYHOATDONG",
                type: "varchar(30)",
                unicode: false,
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HANHDONG",
                table: "NHATKYCONGVIEC",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MANHANVIEN",
                table: "NHATKYCONGVIEC",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "NGAYTAO",
                table: "NHATKYCONGVIEC",
                type: "datetime2(0)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NOIDUNG",
                table: "NHATKYCONGVIEC",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_NHATKYHOATDONG_DOITUONG",
                table: "NHATKYHOATDONG",
                column: "DOITUONG");

            migrationBuilder.CreateIndex(
                name: "IX_NHATKYHOATDONG_HANHDONG",
                table: "NHATKYHOATDONG",
                column: "HANHDONG");

            migrationBuilder.CreateIndex(
                name: "IX_NHATKYHOATDONG_THOIGIAN",
                table: "NHATKYHOATDONG",
                column: "THOIGIAN");

            migrationBuilder.CreateIndex(
                name: "IX_NHATKYCONGVIEC_MANHANVIEN",
                table: "NHATKYCONGVIEC",
                column: "MANHANVIEN");

            migrationBuilder.CreateIndex(
                name: "IX_NHATKYCONGVIEC_NGAYTAO",
                table: "NHATKYCONGVIEC",
                column: "NGAYTAO");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_NHATKYHOATDONG_DOITUONG",
                table: "NHATKYHOATDONG");

            migrationBuilder.DropIndex(
                name: "IX_NHATKYHOATDONG_HANHDONG",
                table: "NHATKYHOATDONG");

            migrationBuilder.DropIndex(
                name: "IX_NHATKYHOATDONG_THOIGIAN",
                table: "NHATKYHOATDONG");

            migrationBuilder.DropIndex(
                name: "IX_NHATKYCONGVIEC_MANHANVIEN",
                table: "NHATKYCONGVIEC");

            migrationBuilder.DropIndex(
                name: "IX_NHATKYCONGVIEC_NGAYTAO",
                table: "NHATKYCONGVIEC");

            migrationBuilder.DropColumn(
                name: "DOITUONG",
                table: "NHATKYHOATDONG");

            migrationBuilder.DropColumn(
                name: "DULIEUCU",
                table: "NHATKYHOATDONG");

            migrationBuilder.DropColumn(
                name: "DULIEUMOI",
                table: "NHATKYHOATDONG");

            migrationBuilder.DropColumn(
                name: "IP",
                table: "NHATKYHOATDONG");

            migrationBuilder.DropColumn(
                name: "TRANGTHAI",
                table: "NHATKYHOATDONG");

            migrationBuilder.DropColumn(
                name: "HANHDONG",
                table: "NHATKYCONGVIEC");

            migrationBuilder.DropColumn(
                name: "MANHANVIEN",
                table: "NHATKYCONGVIEC");

            migrationBuilder.DropColumn(
                name: "NGAYTAO",
                table: "NHATKYCONGVIEC");

            migrationBuilder.DropColumn(
                name: "NOIDUNG",
                table: "NHATKYCONGVIEC");
        }
    }
}
