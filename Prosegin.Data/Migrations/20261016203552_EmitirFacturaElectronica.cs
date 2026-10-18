using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prosegin.Data.Migrations
{
    /// <inheritdoc />
    public partial class EmitirFacturaElectronica : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                SET @facturas_fk = (
                    SELECT CONSTRAINT_NAME
                    FROM information_schema.KEY_COLUMN_USAGE
                    WHERE TABLE_SCHEMA = DATABASE()
                      AND TABLE_NAME = 'Facturas'
                      AND COLUMN_NAME = 'OrdenVentaId'
                      AND REFERENCED_TABLE_NAME = 'OrdenesVenta'
                    LIMIT 1
                );
                SET @drop_fk = IF(
                    @facturas_fk IS NULL,
                    'SELECT 1',
                    CONCAT('ALTER TABLE `Facturas` DROP FOREIGN KEY `', @facturas_fk, '`')
                );
                PREPARE drop_fk_statement FROM @drop_fk;
                EXECUTE drop_fk_statement;
                DEALLOCATE PREPARE drop_fk_statement;
                """);

            migrationBuilder.Sql("""
                SET @old_index = (
                    SELECT INDEX_NAME
                    FROM information_schema.STATISTICS
                    WHERE TABLE_SCHEMA = DATABASE()
                      AND TABLE_NAME = 'Facturas'
                      AND INDEX_NAME = 'IX_Facturas_OrdenVentaId'
                    LIMIT 1
                );
                SET @drop_index = IF(
                    @old_index IS NULL,
                    'SELECT 1',
                    'ALTER TABLE `Facturas` DROP INDEX `IX_Facturas_OrdenVentaId`'
                );
                PREPARE drop_index_statement FROM @drop_index;
                EXECUTE drop_index_statement;
                DEALLOCATE PREPARE drop_index_statement;
                """);

            foreach (var column in new[]
            {
                ("CadenaQr", "varchar(1000) NULL"),
                ("CodigoHash", "varchar(255) NULL"),
                ("EnlaceCdr", "varchar(500) NULL"),
                ("EnlacePdf", "varchar(500) NULL"),
                ("EnlaceXml", "varchar(500) NULL"),
                ("EstadoEmision", "varchar(30) NOT NULL DEFAULT 'Pendiente_Envio'"),
                ("MensajeError", "varchar(1000) NULL"),
                ("Numero", "int NOT NULL DEFAULT 0"),
                ("Serie", "varchar(10) NOT NULL DEFAULT ''")
            })
            {
                var columnDefinition = column.Item2.Replace("'", "''", StringComparison.Ordinal);
                migrationBuilder.Sql($"""
                    SET @column_exists = (
                        SELECT COUNT(*)
                        FROM information_schema.COLUMNS
                        WHERE TABLE_SCHEMA = DATABASE()
                          AND TABLE_NAME = 'Facturas'
                          AND COLUMN_NAME = '{column.Item1}'
                    );
                    SET @add_column = IF(
                        @column_exists = 0,
                        'ALTER TABLE `Facturas` ADD COLUMN `{column.Item1}` {columnDefinition}',
                        'SELECT 1'
                    );
                    PREPARE add_column_statement FROM @add_column;
                    EXECUTE add_column_statement;
                    DEALLOCATE PREPARE add_column_statement;
                    """);
            }

            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS `FacturaDetalles` (
                    `Id` int NOT NULL AUTO_INCREMENT,
                    `FacturaId` int NOT NULL,
                    `Codigo` varchar(50) NOT NULL,
                    `Descripcion` varchar(200) NOT NULL,
                    `UnidadMedida` varchar(10) NOT NULL,
                    `Cantidad` decimal(18,4) NOT NULL,
                    `ValorUnitario` decimal(18,2) NOT NULL,
                    `PrecioUnitario` decimal(18,2) NOT NULL,
                    `Descuento` decimal(18,2) NOT NULL,
                    `Subtotal` decimal(18,2) NOT NULL,
                    `Igv` decimal(18,2) NOT NULL,
                    `Total` decimal(18,2) NOT NULL,
                    CONSTRAINT `PK_FacturaDetalles` PRIMARY KEY (`Id`),
                    CONSTRAINT `FK_FacturaDetalles_Facturas_FacturaId`
                        FOREIGN KEY (`FacturaId`) REFERENCES `Facturas` (`Id`) ON DELETE CASCADE
                ) CHARACTER SET=utf8mb4;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Facturas_OrdenVentaId",
                table: "Facturas",
                column: "OrdenVentaId",
                unique: true);

            migrationBuilder.Sql(
                "UPDATE `Facturas` SET `Serie` = 'LEGACY', `Numero` = `Id` WHERE (`Serie` = '' OR `Serie` IS NULL) AND `Numero` = 0;");

            migrationBuilder.CreateIndex(
                name: "IX_Facturas_Serie_Numero",
                table: "Facturas",
                columns: new[] { "Serie", "Numero" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Facturas_OrdenesVenta_OrdenVentaId",
                table: "Facturas",
                column: "OrdenVentaId",
                principalTable: "OrdenesVenta",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.CreateIndex(
                name: "IX_FacturaDetalles_FacturaId",
                table: "FacturaDetalles",
                column: "FacturaId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FacturaDetalles");

            migrationBuilder.Sql("""
                SET @facturas_fk = (
                    SELECT CONSTRAINT_NAME
                    FROM information_schema.KEY_COLUMN_USAGE
                    WHERE TABLE_SCHEMA = DATABASE()
                      AND TABLE_NAME = 'Facturas'
                      AND COLUMN_NAME = 'OrdenVentaId'
                      AND REFERENCED_TABLE_NAME = 'OrdenesVenta'
                    LIMIT 1
                );
                SET @drop_fk = IF(
                    @facturas_fk IS NULL,
                    'SELECT 1',
                    CONCAT('ALTER TABLE `Facturas` DROP FOREIGN KEY `', @facturas_fk, '`')
                );
                PREPARE drop_fk_statement FROM @drop_fk;
                EXECUTE drop_fk_statement;
                DEALLOCATE PREPARE drop_fk_statement;
                """);

            migrationBuilder.DropIndex(
                name: "IX_Facturas_OrdenVentaId",
                table: "Facturas");

            migrationBuilder.DropIndex(
                name: "IX_Facturas_Serie_Numero",
                table: "Facturas");

            migrationBuilder.DropColumn(
                name: "CadenaQr",
                table: "Facturas");

            migrationBuilder.DropColumn(
                name: "CodigoHash",
                table: "Facturas");

            migrationBuilder.DropColumn(
                name: "EnlaceCdr",
                table: "Facturas");

            migrationBuilder.DropColumn(
                name: "EnlacePdf",
                table: "Facturas");

            migrationBuilder.DropColumn(
                name: "EnlaceXml",
                table: "Facturas");

            migrationBuilder.DropColumn(
                name: "EstadoEmision",
                table: "Facturas");

            migrationBuilder.DropColumn(
                name: "MensajeError",
                table: "Facturas");

            migrationBuilder.DropColumn(
                name: "Numero",
                table: "Facturas");

            migrationBuilder.DropColumn(
                name: "Serie",
                table: "Facturas");

            migrationBuilder.CreateIndex(
                name: "IX_Facturas_OrdenVentaId",
                table: "Facturas",
                column: "OrdenVentaId");

            migrationBuilder.AddForeignKey(
                name: "FK_Facturas_OrdenesVenta_OrdenVentaId",
                table: "Facturas",
                column: "OrdenVentaId",
                principalTable: "OrdenesVenta",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
