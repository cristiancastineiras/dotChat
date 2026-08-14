using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Chat.Infraestructura.Migraciones
{
    /// <inheritdoc />
    public partial class AgregarDuracionAudioAdjuntos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DuracionMs",
                table: "Adjuntos",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DuracionMs",
                table: "Adjuntos");
        }
    }
}
