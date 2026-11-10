using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Prosegin.Data;

#nullable disable

namespace Prosegin.Data.Migrations;

[DbContext(typeof(ProseginDbContext))]
[Migration("20261110110000_RegistrarPagosFactura")]
public partial class RegistrarPagosFactura : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "PagosFactura",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("MySql:ValueGenerationStrategy", Microsoft.EntityFrameworkCore.Metadata.MySqlValueGenerationStrategy.IdentityColumn),
                FacturaId = table.Column<int>(type: "int", nullable: false),
                Monto = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                FechaPago = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                MetodoPago = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                Referencia = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                FechaRegistro = table.Column<DateTime>(type: "datetime(6)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PagosFactura", pago => pago.Id);
                table.ForeignKey(
                    name: "FK_PagosFactura_Facturas_FacturaId",
                    column: pago => pago.FacturaId,
                    principalTable: "Facturas",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            })
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.CreateIndex(
            name: "IX_PagosFactura_FacturaId",
            table: "PagosFactura",
            column: "FacturaId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "PagosFactura");
    }
}
