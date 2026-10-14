using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prosegin.Data.Migrations
{
    /// <inheritdoc />
    public partial class Hu41SnapshotAndConcurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Revision",
                table: "OrdenesVenta",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "NombreProducto",
                table: "CotizacionDetalles",
                type: "varchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.Sql(
                """
                UPDATE CotizacionDetalles
                SET NombreProducto = (
                    SELECT Productos.Nombre
                    FROM Productos
                    WHERE Productos.Id = CotizacionDetalles.ProductoId
                )
                WHERE NombreProducto = ''
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Revision",
                table: "OrdenesVenta");

            migrationBuilder.DropColumn(
                name: "NombreProducto",
                table: "CotizacionDetalles");
        }
    }
}
