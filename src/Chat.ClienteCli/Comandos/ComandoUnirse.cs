using System.ComponentModel;
using Chat.ClienteCli.Servicios;
using Spectre.Console.Cli;

namespace Chat.ClienteCli.Comandos;

/// <summary>
/// Se une a una sala y abre la conversación en vivo: muestra el historial reciente,
/// recibe los mensajes nuevos por SignalR en tiempo real y permite escribir.
/// </summary>
public sealed class ComandoUnirse : ComandoBase<ComandoUnirse.Opciones>
{
    private readonly ClienteApi _api;
    private readonly VistaConversacion _vista;

    /// <summary>Crea el comando.</summary>
    /// <param name="api">Cliente de la API.</param>
    /// <param name="vista">Vista de conversación en vivo.</param>
    public ComandoUnirse(ClienteApi api, VistaConversacion vista)
    {
        _api = api;
        _vista = vista;
    }

    /// <summary>Opciones del comando <c>unirse</c>.</summary>
    public sealed class Opciones : CommandSettings
    {
        /// <summary>Nombre de la sala.</summary>
        [CommandArgument(0, "<sala>")]
        [Description("Nombre de la sala a la que unirse.")]
        public string Sala { get; init; } = string.Empty;

        /// <summary>Solo une a la sala, sin abrir la conversación en vivo.</summary>
        [CommandOption("--sin-chat")]
        [Description("Solo registra la pertenencia a la sala, sin abrir la conversación en vivo.")]
        public bool SinChat { get; init; }
    }

    /// <inheritdoc />
    protected override async Task<int> EjecutarAsync(Opciones opciones, CancellationToken cancelacion)
    {
        var sesion = await _api.RequerirSesionAsync(cancelacion).ConfigureAwait(false);
        var sala = await _api.ResolverSalaAsync(opciones.Sala, cancelacion).ConfigureAwait(false);

        // Unirse es idempotente; en una sala privada de la que ya se es miembro
        // simplemente devuelve la sala.
        var abierta = await _api.UnirseSalaAsync(sala.Id, cancelacion).ConfigureAwait(false);
        Presentacion.Exito($"Te has unido a la sala '{abierta.Nombre}'.");

        if (opciones.SinChat)
        {
            return CodigoExito;
        }

        await _vista.AbrirAsync(sesion, abierta, cancelacion).ConfigureAwait(false);
        return CodigoExito;
    }
}
