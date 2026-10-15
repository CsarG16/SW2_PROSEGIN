using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prosegin.Data.Migrations
{
    /// <inheritdoc />
    public partial class LimitarActualizacionCotizacion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "FueActualizada",
                table: "Cotizaciones",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FueActualizada",
                table: "Cotizaciones");
        }
    }
}
