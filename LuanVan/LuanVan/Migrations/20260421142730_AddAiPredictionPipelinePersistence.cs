using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LuanVan.Migrations
{
    /// <inheritdoc />
    public partial class AddAiPredictionPipelinePersistence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DUDOANAI_MANHANVIEN",
                table: "DUDOANAI");

            migrationBuilder.AddColumn<string>(
                name: "ACTOR",
                table: "DUDOANAI",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "INPUTDATA",
                table: "DUDOANAI",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MODELNAME",
                table: "DUDOANAI",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OUTPUTDATA",
                table: "DUDOANAI",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "UQ_DUDOANAI_NV_MODEL_THANG_NAM",
                table: "DUDOANAI",
                columns: new[] { "MANHANVIEN", "MODELNAME", "THANG", "NAM" },
                unique: true,
                filter: "[MODELNAME] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UQ_DUDOANAI_NV_MODEL_THANG_NAM",
                table: "DUDOANAI");

            migrationBuilder.DropColumn(
                name: "ACTOR",
                table: "DUDOANAI");

            migrationBuilder.DropColumn(
                name: "INPUTDATA",
                table: "DUDOANAI");

            migrationBuilder.DropColumn(
                name: "MODELNAME",
                table: "DUDOANAI");

            migrationBuilder.DropColumn(
                name: "OUTPUTDATA",
                table: "DUDOANAI");

            migrationBuilder.CreateIndex(
                name: "IX_DUDOANAI_MANHANVIEN",
                table: "DUDOANAI",
                column: "MANHANVIEN");
        }
    }
}
