using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prosegin.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddClienteReferencia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LimiteCredito",
                table: "Clientes");

            migrationBuilder.DropColumn(
                name: "RepresentanteLegal",
                table: "Clientes");

            migrationBuilder.AddColumn<string>(
                name: "ContactoRecepcion",
                table: "PuntosEntrega",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Departamento",
                table: "PuntosEntrega",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Distrito",
                table: "PuntosEntrega",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<bool>(
                name: "EsPredeterminada",
                table: "PuntosEntrega",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "HorarioRecepcion",
                table: "PuntosEntrega",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "NombreAlias",
                table: "PuntosEntrega",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Provincia",
                table: "PuntosEntrega",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "RestriccionesAcceso",
                table: "PuntosEntrega",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "TelefonoMovil",
                table: "PuntosEntrega",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "TipoSede",
                table: "PuntosEntrega",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Ubigeo",
                table: "PuntosEntrega",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Referencia",
                table: "Clientes",
                type: "varchar(250)",
                maxLength: 250,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ContactoRecepcion",
                table: "PuntosEntrega");

            migrationBuilder.DropColumn(
                name: "Departamento",
                table: "PuntosEntrega");

            migrationBuilder.DropColumn(
                name: "Distrito",
                table: "PuntosEntrega");

            migrationBuilder.DropColumn(
                name: "EsPredeterminada",
                table: "PuntosEntrega");

            migrationBuilder.DropColumn(
                name: "HorarioRecepcion",
                table: "PuntosEntrega");

            migrationBuilder.DropColumn(
                name: "NombreAlias",
                table: "PuntosEntrega");

            migrationBuilder.DropColumn(
                name: "Provincia",
                table: "PuntosEntrega");

            migrationBuilder.DropColumn(
                name: "RestriccionesAcceso",
                table: "PuntosEntrega");

            migrationBuilder.DropColumn(
                name: "TelefonoMovil",
                table: "PuntosEntrega");

            migrationBuilder.DropColumn(
                name: "TipoSede",
                table: "PuntosEntrega");

            migrationBuilder.DropColumn(
                name: "Ubigeo",
                table: "PuntosEntrega");

            migrationBuilder.DropColumn(
                name: "Referencia",
                table: "Clientes");

            migrationBuilder.AddColumn<decimal>(
                name: "LimiteCredito",
                table: "Clientes",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "RepresentanteLegal",
                table: "Clientes",
                type: "varchar(200)",
                maxLength: 200,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }
    }
}
