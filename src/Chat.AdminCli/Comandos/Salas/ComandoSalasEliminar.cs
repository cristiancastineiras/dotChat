using System.ComponentModel;
using Chat.AdminCli.Servicios;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Chat.AdminCli.Comandos.Salas;

/// <summary>
/// Comando <c>salas eliminar</c>: borra una sala y, en cascada, su historial completo.
/// </summary>
public sealed class ComandoSalasEliminar : ComandoAdminBase<ComandoSalasEliminar.Opciones>
{
    /// <summary>Crea el comando.</summary>
    /// <param name="api">Cliente de la API administrativa.</param>
    public ComandoSalasEliminar(ClienteAdminApi api) : base(api)
    {
    }

    /// <summary>Opciones del comando.</summary>
    public sealed class Opciones : CommandSettings
    {
        /// <summary>Nombre o identificador de la sala.</summary>
        [CommandArgument(0, "<sala>")]
        [Description("Nombre o identificador (GUID) de la sala a eliminar.")]
        public string Sala { get; init; } = string.Empty;

        /// <summary>Omite la confirmación interactiva.</summary>
        [CommandOption("-y|--si")]
        [Description("No pide confirmación (para uso en guiones).")]
        public bool SinConfirmar { get; init; }
    }

    /// <inheritdoc />
    protected override async Task<int> EjecutarAsync(Opciones opciones, CancellationToken cancelacion)
    {
        PresentacionAdmin.Cabecera($"Eliminación de sala en {Api.UrlServidor}");

        var (salaId, nombre) = Guid.TryParse(opciones.Sala, out var identificador)
            ? (identificador, opciones.Sala)
            : await ResolverPorNombreAsync(opciones.Sala, cancelacion).ConfigureAwait(false);

        if (!opciones.SinConfirmar
            && !AnsiConsole.Confirm(
                $"[red]¿Eliminar la sala '{PresentacionAdmin.Escapar(nombre)}' y todo su historial?[/]",
                false))
        {
            PresentacionAdmin.Aviso("Operación cancelada por el usuario.");
            return CodigoError;
        }

        var resultado = await ConEsperaAsync(
            "Eliminando la sala...",
            () => Api.EliminarSalaAsync(salaId, cancelacion)).ConfigureAwait(false);

        PresentacionAdmin.Exito(resultado.Mensaje);
        return CodigoExito;
    }

    /// <summary>Resuelve el identificador de una sala a partir de su nombre.</summary>
    /// <param name="nombre">Nombre buscado.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    private async Task<(Guid Id, string Nombre)> ResolverPorNombreAsync(string nombre, CancellationToken cancelacion)
    {
        var sala = await Api.ResolverSalaAsync(nombre, cancelacion).ConfigureAwait(false);
        return (sala.Id, sala.Nombre);
    }
}
