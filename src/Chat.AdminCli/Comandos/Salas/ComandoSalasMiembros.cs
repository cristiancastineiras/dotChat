using System.ComponentModel;
using Chat.AdminCli.Servicios;
using Spectre.Console.Cli;

namespace Chat.AdminCli.Comandos.Salas;

/// <summary>
/// Comando <c>salas miembros</c>: muestra quién compone una sala y quién está
/// conectado en este momento.
/// </summary>
public sealed class ComandoSalasMiembros : ComandoAdminBase<ComandoSalasMiembros.Opciones>
{
    /// <summary>Crea el comando.</summary>
    /// <param name="api">Cliente de la API administrativa.</param>
    public ComandoSalasMiembros(ClienteAdminApi api) : base(api)
    {
    }

    /// <summary>Opciones del comando.</summary>
    public sealed class Opciones : CommandSettings
    {
        /// <summary>Nombre o identificador de la sala.</summary>
        [CommandArgument(0, "<sala>")]
        [Description("Nombre o identificador (GUID) de la sala.")]
        public string Sala { get; init; } = string.Empty;
    }

    /// <inheritdoc />
    protected override async Task<int> EjecutarAsync(Opciones opciones, CancellationToken cancelacion)
    {
        PresentacionAdmin.Cabecera($"Composición de sala en {Api.UrlServidor}");

        var (salaId, nombre) = Guid.TryParse(opciones.Sala, out var identificador)
            ? (identificador, opciones.Sala)
            : await ResolverPorNombreAsync(opciones.Sala, cancelacion).ConfigureAwait(false);

        var miembros = await ConEsperaAsync(
            "Consultando miembros...",
            () => Api.ListarMiembrosAsync(salaId, cancelacion)).ConfigureAwait(false);

        PresentacionAdmin.TablaMiembros(miembros, nombre);
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
