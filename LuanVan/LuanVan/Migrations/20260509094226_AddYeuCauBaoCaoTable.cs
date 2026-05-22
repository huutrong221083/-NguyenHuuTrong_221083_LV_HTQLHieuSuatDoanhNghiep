using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LuanVan.Migrations
{
    /// <inheritdoc />
    public partial class AddYeuCauBaoCaoTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "GHICHU",
                table: "NHATKYCONGVIEC",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(300)",
                oldMaxLength: 300);

            migrationBuilder.AlterColumn<string>(
                name: "TENTAILIEU",
                table: "CONGVIEC_TAILIEU",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "NGUOITAO",
                table: "CONGVIEC_TAILIEU",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128);

            migrationBuilder.AlterColumn<string>(
                name: "LOAIFILE",
                table: "CONGVIEC_TAILIEU",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "DUONGDAN",
                table: "CONGVIEC_TAILIEU",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500);

            migrationBuilder.AlterColumn<string>(
                name: "TENCONGVIEC",
                table: "CONGVIEC",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "NGUOITAO",
                table: "CONGVIEC",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128);

            migrationBuilder.AlterColumn<string>(
                name: "NGUOICAPNHAT",
                table: "CONGVIEC",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128);

            migrationBuilder.AlterColumn<string>(
                name: "MOTA",
                table: "CONGVIEC",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(300)",
                oldMaxLength: 300);

            migrationBuilder.CreateTable(
                name: "YEUCAUBAOCAO",
                columns: table => new
                {
                    MAYEUCAU = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NGUOIYEUCAU = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    NGUOINHAN = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    TIEUDE = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    MOTA = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PRIORITY = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    HANCHOT = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TRANGTHAI = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    NGAYTAO = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NGAYCAPNHAT = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ISDELETED = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YEUCAUBAOCAO", x => x.MAYEUCAU);
                    table.ForeignKey(
                        name: "FK_YEUCAU_EMPLOYEE",
                        column: x => x.NGUOINHAN,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_YEUCAU_MANAGER",
                        column: x => x.NGUOIYEUCAU,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_YEUCAUBAOCAO_NGUOINHAN",
                table: "YEUCAUBAOCAO",
                column: "NGUOINHAN");

            migrationBuilder.CreateIndex(
                name: "IX_YEUCAUBAOCAO_NGUOIYEUCAU",
                table: "YEUCAUBAOCAO",
                column: "NGUOIYEUCAU");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "YEUCAUBAOCAO");

            migrationBuilder.RenameTable(
                name: "BAOCAOCHITIET_PORTAL",
                newName: "BAOCAOCHITIET");

            migrationBuilder.RenameTable(
                name: "BAOCAO_PORTAL",
                newName: "BAOCAO");

            migrationBuilder.RenameIndex(
                name: "IX_BAOCAOCHITIET_PORTAL_MABAOCAO",
                table: "BAOCAOCHITIET",
                newName: "IX_BAOCAOCHITIET_MABAOCAO");

            migrationBuilder.RenameIndex(
                name: "IX_BAOCAO_PORTAL_NGUOITAO",
                table: "BAOCAO",
                newName: "IX_BAOCAO_NGUOITAO");

            migrationBuilder.RenameIndex(
                name: "IX_BAOCAO_PORTAL_MAPHONGBAN",
                table: "BAOCAO",
                newName: "IX_BAOCAO_MAPHONGBAN");

            migrationBuilder.RenameIndex(
                name: "IX_BAOCAO_PORTAL_MADUAN",
                table: "BAOCAO",
                newName: "IX_BAOCAO_MADUAN");

            migrationBuilder.AlterColumn<string>(
                name: "GHICHU",
                table: "NHATKYCONGVIEC",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(300)",
                oldMaxLength: 300,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "TENTAILIEU",
                table: "CONGVIEC_TAILIEU",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "NGUOITAO",
                table: "CONGVIEC_TAILIEU",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "LOAIFILE",
                table: "CONGVIEC_TAILIEU",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "DUONGDAN",
                table: "CONGVIEC_TAILIEU",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "TENCONGVIEC",
                table: "CONGVIEC",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "NGUOITAO",
                table: "CONGVIEC",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "NGUOICAPNHAT",
                table: "CONGVIEC",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "MOTA",
                table: "CONGVIEC",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(300)",
                oldMaxLength: 300,
                oldNullable: true);
        }
    }
}
