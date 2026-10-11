using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prosegin.Data.Migrations;

public partial class AgregarTarifasPorProveedor : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<decimal>(
            name: "CostoCompra",
            table: "ProductoProveedores",
            type: "decimal(18,2)",
            precision: 18,
            scale: 2,
            nullable: false,
            defaultValue: 0m);

        migrationBuilder.AddColumn<bool>(
            name: "EsPrincipal",
            table: "ProductoProveedores",
            type: "tinyint(1)",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<int>(
            name: "PlazoEntregaHoras",
            table: "ProductoProveedores",
            type: "int",
            nullable: false,
            defaultValue: 24);

        migrationBuilder.Sql(
            """
            UPDATE ProductoProveedores pp
            INNER JOIN Productos p ON p.Id = pp.ProductoId
            SET pp.CostoCompra = p.CostoReferencial
            """);

        migrationBuilder.Sql(
            """
            UPDATE ProductoProveedores pp
            INNER JOIN (
                SELECT ProductoId, MIN(ProveedorId) AS ProveedorId
                FROM (
                    SELECT ProductoId, ProveedorId
                    FROM ProductoProveedores
                ) existentes
                GROUP BY ProductoId
            ) principales ON principales.ProductoId = pp.ProductoId
            SET pp.EsPrincipal = pp.ProveedorId = principales.ProveedorId
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "CostoCompra", table: "ProductoProveedores");
        migrationBuilder.DropColumn(name: "EsPrincipal", table: "ProductoProveedores");
        migrationBuilder.DropColumn(name: "PlazoEntregaHoras", table: "ProductoProveedores");
    }
}
