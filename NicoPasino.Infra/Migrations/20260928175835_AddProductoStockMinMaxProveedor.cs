using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NicoPasino.Infra.Migrations
{
    /// <inheritdoc />
    public partial class AddProductoStockMinMaxProveedor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "proveedor",
                table: "producto",
                type: "varchar(150)",
                maxLength: 150,
                nullable: true,
                collation: "utf8mb4_0900_ai_ci")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "stockMaximo",
                table: "producto",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "stockMinimo",
                table: "producto",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "proveedor",
                table: "producto");

            migrationBuilder.DropColumn(
                name: "stockMaximo",
                table: "producto");

            migrationBuilder.DropColumn(
                name: "stockMinimo",
                table: "producto");
        }
    }
}
