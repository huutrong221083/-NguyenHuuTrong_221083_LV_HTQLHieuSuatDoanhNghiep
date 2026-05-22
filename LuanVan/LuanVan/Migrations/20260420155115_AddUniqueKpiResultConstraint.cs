using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LuanVan.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueKpiResultConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "UQ_KETQUAKPI_NV_KPI_THANG_NAM",
                table: "KETQUAKPI",
                columns: new[] { "MANHANVIEN", "MAKPI", "THANG", "NAM" },
                unique: true,
                filter: "[THANG] IS NOT NULL AND [NAM] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UQ_KETQUAKPI_NV_KPI_THANG_NAM",
                table: "KETQUAKPI");
        }
    }
}
