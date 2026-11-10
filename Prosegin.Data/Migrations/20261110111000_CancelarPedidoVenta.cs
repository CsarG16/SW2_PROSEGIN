using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Prosegin.Data;

#nullable disable

namespace Prosegin.Data.Migrations;

[DbContext(typeof(ProseginDbContext))]
[Migration("20261110111000_CancelarPedidoVenta")]
public partial class CancelarPedidoVenta : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "MotivoCancelacion",
            table: "OrdenesVenta",
            type: "varchar(500)",
            maxLength: 500,
            nullable: true)
            .Annotation("MySql:CharSet", "utf8mb4");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "MotivoCancelacion",
            table: "OrdenesVenta");
    }
}
