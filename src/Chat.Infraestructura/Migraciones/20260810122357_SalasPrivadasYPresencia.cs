using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Chat.Infraestructura.Migraciones
{
    /// <inheritdoc />
    public partial class SalasPrivadasYPresencia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ClaveDirecta",
                table: "Salas",
                type: "TEXT",
                maxLength: 65,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "FechaUltimaActividad",
                table: "Salas",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Tipo",
                table: "Salas",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "FechaUltimaLectura",
                table: "MiembrosSala",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Salas_ClaveDirecta",
                table: "Salas",
                column: "ClaveDirecta",
                unique: true,
                filter: "\"ClaveDirecta\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Salas_Tipo_FechaUltimaActividad",
                table: "Salas",
                columns: new[] { "Tipo", "FechaUltimaActividad" },
                descending: new[] { false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Salas_ClaveDirecta",
                table: "Salas");

            migrationBuilder.DropIndex(
                name: "IX_Salas_Tipo_FechaUltimaActividad",
                table: "Salas");

            migrationBuilder.DropColumn(
                name: "ClaveDirecta",
                table: "Salas");

            migrationBuilder.DropColumn(
                name: "FechaUltimaActividad",
                table: "Salas");

            migrationBuilder.DropColumn(
                name: "Tipo",
                table: "Salas");

            migrationBuilder.DropColumn(
                name: "FechaUltimaLectura",
                table: "MiembrosSala");
        }
    }
}
