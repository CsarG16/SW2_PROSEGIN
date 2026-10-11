using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prosegin.Data.Migrations
{
    /// <inheritdoc />
    public partial class AgregarOrdenCompraCliente : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NombreArchivoOrdenCompraCliente",
                table: "Cotizaciones",
                type: "varchar(255)",
                maxLength: 255,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "NumeroOrdenCompraCliente",
                table: "Cotizaciones",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "RutaOrdenCompraCliente",
                table: "Cotizaciones",
                type: "varchar(40)",
                maxLength: 40,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NombreArchivoOrdenCompraCliente",
                table: "Cotizaciones");

            migrationBuilder.DropColumn(
                name: "NumeroOrdenCompraCliente",
                table: "Cotizaciones");

            migrationBuilder.DropColumn(
                name: "RutaOrdenCompraCliente",
                table: "Cotizaciones");
        }
    }
}
