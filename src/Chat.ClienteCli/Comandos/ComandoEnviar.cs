using System.ComponentModel;
using Chat.ClienteCli.Servicios;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Chat.ClienteCli.Comandos;

/// <summary>
/// Envía un mensaje puntual a una sala sin abrir la conversación en vivo.
/// Útil para guiones y automatizaciones.
/// </summary>
public sealed class ComandoEnviar : ComandoBase<ComandoEnviar.Opciones>
{
    private readonly ClienteApi _api;

    /// <summary>Crea el comando.</summary>
    /// <param name="api">Cliente de la API.</param>
    public ComandoEnviar(ClienteApi api) => _api = api;

    /// <summary>Opciones del comando <c>enviar</c>.</summary>
    public sealed class Opciones : CommandSettings
    {
        /// <summary>Sala destino.</summary>
        [CommandArgument(0, "<sala>")]
        [Description("Nombre de la sala destino.")]
        public string Sala { get; init; } = string.Empty;

        /// <summary>Texto del mensaje.</summary>
        [CommandArgument(1, "<mensaje>")]
        [Description("Texto del mensaje. Enciérrelo entre comillas si contiene espacios.")]
        public string Mensaje { get; init; } = string.Empty;
    }

    /// <inheritdoc />
    protected override async Task<int> EjecutarAsync(Opciones opciones, CancellationToken cancelacion)
    {
        await _api.RequerirSesionAsync(cancelacion).ConfigureAwait(false);
        var sala = await _api.ResolverSalaAsync(opciones.Sala, cancelacion).ConfigureAwait(false);

        var mensaje = await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(Style.Parse("deepskyblue1"))
            .StartAsync(
                $"Enviando a '{sala.Nombre}'...",
                async _ => await _api.EnviarMensajeAsync(sala.Id, opciones.Mensaje, cancelacion).ConfigureAwait(false))
            .ConfigureAwait(false);

        Presentacion.Exito($"Mensaje enviado a '{sala.Nombre}'.");
        Presentacion.LineaMensaje(mensaje, esPropio: true);

        return CodigoExito;
    }
}
