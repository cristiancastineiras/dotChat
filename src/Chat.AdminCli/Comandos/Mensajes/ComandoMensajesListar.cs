using System.ComponentModel;
using Chat.AdminCli.Servicios;
using Spectre.Console.Cli;

namespace Chat.AdminCli.Comandos.Mensajes;

/// <summary>
/// Comando <c>mensajes listar</c>: audita el historial de una sala. Los mensajes se
/// descifran en el servidor y viajan por HTTPS únicamente hacia el administrador.
/// </summary>
public sealed class ComandoMensajesListar : ComandoAdminBase<ComandoMensajesListar.Opciones>
{
    /// <summary>Crea el comando.</summary>
    /// <param name="api">Cliente de la API administrativa.</param>
    public ComandoMensajesListar(ClienteAdminApi api) : base(api)
    {
    }

    /// <summary>Opciones del comando.</summary>
    public sealed class Opciones : CommandSettings
    {
        /// <summary>Sala consultada.</summary>
        [CommandArgument(0, "<sala>")]
        [Description("Nombre o identificador (GUID) de la sala.")]
        public string Sala { get; init; } = string.Empty;

        /// <summary>Número de mensajes a mostrar.</summary>
        [CommandOption("-n|--cantidad")]
        [Description("Número de mensajes a mostrar (por defecto 50, máximo 200).")]
        [DefaultValue(50)]
        public int Cantidad { get; init; } = 50;
    }

    /// <inheritdoc />
    protected override async Task<int> EjecutarAsync(Opciones opciones, CancellationToken cancelacion)
    {
        PresentacionAdmin.Cabecera($"Auditoría de mensajes en {Api.UrlServidor}");

        var (salaId, nombre) = Guid.TryParse(opciones.Sala, out var identificador)
            ? (identificador, opciones.Sala)
            : await ResolverPorNombreAsync(opciones.Sala, cancelacion).ConfigureAwait(false);

        var mensajes = await ConEsperaAsync(
            "Descargando y descifrando mensajes...",
            () => Api.ListarMensajesAsync(salaId, opciones.Cantidad, cancelacion)).ConfigureAwait(false);

        PresentacionAdmin.TablaMensajes(mensajes, nombre);
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
