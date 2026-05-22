using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LuanVan.Migrations
{
    public partial class AddYeuCauCapNhatHoSoTable : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "YEUCAU_CAPNHAT_HOSO",
                columns: table => new
                {
                    MAYEUCAU = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MANHANVIEN = table.Column<int>(type: "int", nullable: false),
                    TRANGTHAI = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false, defaultValue: "ChoDuyet"),
                    DANHSACH_TRUONG = table.Column<string>(type: "varchar(200)", unicode: false, maxLength: 200, nullable: true),
                    DULIEU_CU_JSON = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DULIEU_MOI_JSON = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LYDO_GUI = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    LYDO_TUCHOI = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    GHICHU_DUYET = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    NGUOITAO = table.Column<int>(type: "int", nullable: true),
                    NGUOIDUYET = table.Column<int>(type: "int", nullable: true),
                    NGUOICAPNHAT = table.Column<int>(type: "int", nullable: true),
                    NGAYTAO = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    NGAYDUYET = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    NGAYCAPNHAT = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    IP_TAO = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: true),
                    IP_DUYET = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: true),
                    ISDELETED = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YEUCAU_CAPNHAT_HOSO", x => x.MAYEUCAU);
                    table.ForeignKey(
                        name: "FK_YEUCAU_CAPNHAT_HOSO_NGUOIDUYET",
                        column: x => x.NGUOIDUYET,
                        principalTable: "NHANVIEN",
                        principalColumn: "MANHANVIEN",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_YEUCAU_CAPNHAT_HOSO_NHANVIEN",
                        column: x => x.MANHANVIEN,
                        principalTable: "NHANVIEN",
                        principalColumn: "MANHANVIEN");
                });

            migrationBuilder.CreateIndex(
                name: "IX_YEUCAU_CAPNHAT_HOSO_MANHANVIEN",
                table: "YEUCAU_CAPNHAT_HOSO",
                column: "MANHANVIEN");

            migrationBuilder.CreateIndex(
                name: "IX_YEUCAU_CAPNHAT_HOSO_NGAYTAO",
                table: "YEUCAU_CAPNHAT_HOSO",
                column: "NGAYTAO");

            migrationBuilder.CreateIndex(
                name: "IX_YEUCAU_CAPNHAT_HOSO_NGUOIDUYET",
                table: "YEUCAU_CAPNHAT_HOSO",
                column: "NGUOIDUYET");

            migrationBuilder.CreateIndex(
                name: "IX_YEUCAU_CAPNHAT_HOSO_TRANGTHAI",
                table: "YEUCAU_CAPNHAT_HOSO",
                column: "TRANGTHAI");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "YEUCAU_CAPNHAT_HOSO");
        }
    }
}
