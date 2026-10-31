using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prosegin.Data.Migrations
{
    /// <inheritdoc />
    public partial class AgregarCorrelativoOrdenCompra : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Correlativo",
                table: "OrdenesCompra",
                type: "varchar(30)",
                maxLength: 30,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.Sql(
                "UPDATE `OrdenesCompra` SET `Correlativo` = CONCAT('OC-PROV-', 1043 + `Id`) WHERE `Correlativo` IS NULL OR `Correlativo` = '';");

            migrationBuilder.AlterColumn<string>(
                name: "Correlativo",
                table: "OrdenesCompra",
                type: "varchar(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(30)",
                oldMaxLength: 30,
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_OrdenesCompra_Correlativo",
                table: "OrdenesCompra",
                column: "Correlativo",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OrdenesCompra_Correlativo",
                table: "OrdenesCompra");

            migrationBuilder.DropColumn(
                name: "Correlativo",
                table: "OrdenesCompra");
        }
    }
}
