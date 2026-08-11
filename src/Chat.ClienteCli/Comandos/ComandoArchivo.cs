using System.ComponentModel;
using Chat.ClienteCli.Servicios;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Chat.ClienteCli.Comandos;

/// <summary>
/// Comparte un archivo en una sala sin abrir la conversación en vivo.
/// </summary>
/// <remarks>
/// Va en dos pasos, igual que dentro de la conversación: primero se sube el fichero
/// por HTTP —donde el servidor lo valida, lo cifra y lo guarda en el almacén de
/// objetos— y después se publica el mensaje que lo referencia.
/// </remarks>
public sealed class ComandoArchivo : ComandoBase<ComandoArchivo.Opciones>
{
    private readonly ClienteApi _api;
    private readonly RenderizadorImagenes _renderizador;

    /// <summary>Crea el comando.</summary>
    /// <param name="api">Cliente de la API.</param>
    /// <param name="renderizador">Dibujante de imágenes en la consola.</param>
    public ComandoArchivo(ClienteApi api, RenderizadorImagenes renderizador)
    {
        _api = api;
        _renderizador = renderizador;
    }

    /// <summary>Opciones del comando <c>archivo</c>.</summary>
    public sealed class Opciones : CommandSettings
    {
        /// <summary>Sala destino.</summary>
        [CommandArgument(0, "<sala>")]
        [Description("Nombre de la sala destino.")]
        public string Sala { get; init; } = string.Empty;

        /// <summary>Ruta del archivo.</summary>
        [CommandArgument(1, "<ruta>")]
        [Description("Ruta del archivo a compartir. Las imágenes se normalizan y se dibujan.")]
        public string Ruta { get; init; } = string.Empty;

        /// <summary>Texto opcional que acompaña al archivo.</summary>
        [CommandOption("-m|--mensaje <TEXTO>")]
        [Description("Texto opcional que acompaña al archivo. Admite atajos de emoji como :fuego:.")]
        public string? Mensaje { get; init; }

        /// <summary>Omite el dibujo de la imagen tras enviarla.</summary>
        [CommandOption("--sin-vista-previa")]
        [Description("No dibuja la imagen en la consola después de enviarla.")]
        public bool SinVistaPrevia { get; init; }
    }

    /// <inheritdoc />
    protected override async Task<int> EjecutarAsync(Opciones opciones, CancellationToken cancelacion)
    {
        await _api.RequerirSesionAsync(cancelacion).ConfigureAwait(false);
        var sala = await _api.ResolverSalaAsync(opciones.Sala, cancelacion).ConfigureAwait(false);

        var mensaje = await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(Style.Parse(Presentacion.ColorPrincipal))
            .StartAsync(
                $"Enviando a '{sala.Nombre}'...",
                async _ =>
                {
                    var adjunto = await _api
                        .SubirAdjuntoAsync(sala.Id, opciones.Ruta, cancelacion)
                        .ConfigureAwait(false);

                    return await _api
                        .EnviarMensajeAsync(sala.Id, opciones.Mensaje ?? string.Empty, adjunto.Id, cancelacion)
                        .ConfigureAwait(false);
                })
            .ConfigureAwait(false);

        Presentacion.Exito($"Archivo enviado a '{sala.Nombre}'.");
        Presentacion.LineaMensaje(mensaje, esPropio: true);

        if (!opciones.SinVistaPrevia && mensaje.Adjunto is { EsImagen: true } adjuntoImagen)
        {
            await _renderizador.DibujarAsync(adjuntoImagen, cancelacion).ConfigureAwait(false);
        }

        return CodigoExito;
    }
}
