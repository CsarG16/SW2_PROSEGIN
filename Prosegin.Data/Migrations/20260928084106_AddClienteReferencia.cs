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
            void DropColumnIfExists(string table, string column)
            {
                migrationBuilder.Sql($@"
                    SET @dbname = DATABASE();
                    SET @tablename = '{table}';
                    SET @colname = '{column}';
                    SET @sql = (SELECT IF(
                        (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = @dbname AND TABLE_NAME = @tablename AND COLUMN_NAME = @colname) > 0,
                        'ALTER TABLE `{table}` DROP COLUMN `{column}`',
                        'SELECT 1'
                    ));
                    PREPARE stmt FROM @sql;
                    EXECUTE stmt;
                    DEALLOCATE PREPARE stmt;
                ");
            }

            void AddColumnIfNotExists(string table, string column, string columnDef)
            {
                migrationBuilder.Sql($@"
                    SET @dbname = DATABASE();
                    SET @tablename = '{table}';
                    SET @colname = '{column}';
                    SET @sql = (SELECT IF(
                        (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = @dbname AND TABLE_NAME = @tablename AND COLUMN_NAME = @colname) = 0,
                        'ALTER TABLE `{table}` ADD `{column}` {columnDef}',
                        'SELECT 1'
                    ));
                    PREPARE stmt FROM @sql;
                    EXECUTE stmt;
                    DEALLOCATE PREPARE stmt;
                ");
            }

            DropColumnIfExists("Clientes", "LimiteCredito");
            DropColumnIfExists("Clientes", "RepresentanteLegal");

            AddColumnIfNotExists("PuntosEntrega", "ContactoRecepcion", "longtext CHARACTER SET utf8mb4 NULL");
            AddColumnIfNotExists("PuntosEntrega", "Departamento", "longtext CHARACTER SET utf8mb4 NULL");
            AddColumnIfNotExists("PuntosEntrega", "Distrito", "longtext CHARACTER SET utf8mb4 NULL");
            AddColumnIfNotExists("PuntosEntrega", "EsPredeterminada", "tinyint(1) NOT NULL DEFAULT 0");
            AddColumnIfNotExists("PuntosEntrega", "HorarioRecepcion", "longtext CHARACTER SET utf8mb4 NULL");
            AddColumnIfNotExists("PuntosEntrega", "NombreAlias", "longtext CHARACTER SET utf8mb4 NULL");
            AddColumnIfNotExists("PuntosEntrega", "Provincia", "longtext CHARACTER SET utf8mb4 NULL");
            AddColumnIfNotExists("PuntosEntrega", "RestriccionesAcceso", "longtext CHARACTER SET utf8mb4 NULL");
            AddColumnIfNotExists("PuntosEntrega", "TelefonoMovil", "longtext CHARACTER SET utf8mb4 NULL");
            AddColumnIfNotExists("PuntosEntrega", "TipoSede", "longtext CHARACTER SET utf8mb4 NULL");
            AddColumnIfNotExists("PuntosEntrega", "Ubigeo", "longtext CHARACTER SET utf8mb4 NULL");

            AddColumnIfNotExists("Clientes", "Referencia", "varchar(250) CHARACTER SET utf8mb4 NULL");
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
