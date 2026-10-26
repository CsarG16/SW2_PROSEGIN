using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prosegin.Data.Migrations
{
    /// <inheritdoc />
    public partial class AgregarCamposComercialesYMonedaProveedor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CondicionPago",
                table: "Proveedores",
                type: "varchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Especialidad",
                table: "Proveedores",
                type: "varchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "NombreComercial",
                table: "Proveedores",
                type: "varchar(150)",
                maxLength: 150,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<bool>(
                name: "EsPrincipal",
                table: "CuentasBancariasProveedor",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Moneda",
                table: "CuentasBancariasProveedor",
                type: "varchar(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "PEN")
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CondicionPago",
                table: "Proveedores");

            migrationBuilder.DropColumn(
                name: "Especialidad",
                table: "Proveedores");

            migrationBuilder.DropColumn(
                name: "NombreComercial",
                table: "Proveedores");

            migrationBuilder.DropColumn(
                name: "EsPrincipal",
                table: "CuentasBancariasProveedor");

            migrationBuilder.DropColumn(
                name: "Moneda",
                table: "CuentasBancariasProveedor");
        }
    }
}
