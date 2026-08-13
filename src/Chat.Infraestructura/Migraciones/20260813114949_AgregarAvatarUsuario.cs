using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Chat.Infraestructura.Migraciones
{
    /// <inheritdoc />
    public partial class AgregarAvatarUsuario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AvatarActualizado",
                table: "Usuarios",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AvatarClaveObjeto",
                table: "Usuarios",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AvatarTipoMime",
                table: "Usuarios",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AvatarActualizado",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "AvatarClaveObjeto",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "AvatarTipoMime",
                table: "Usuarios");
        }
    }
}
