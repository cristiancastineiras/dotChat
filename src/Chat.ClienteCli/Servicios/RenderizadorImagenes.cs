using Chat.Aplicacion.Dtos;
using Microsoft.Extensions.Options;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace Chat.ClienteCli.Servicios;

/// <summary>
/// Descarga y dibuja en la consola las imágenes adjuntas a los mensajes.
/// </summary>
/// <remarks>
/// <para>
/// Un terminal no pinta píxeles, así que la imagen se traduce a caracteres de bloque
/// coloreados: cada celda de texto lleva dos «píxeles» —uno en el color de fondo y otro
/// en el de primer plano— y el resultado es reconocible a partir de unas cuarenta
/// columnas. Funciona en cualquier terminal con color verdadero, sin depender de que
/// admita protocolos gráficos como Sixel o el de Kitty.
/// </para>
/// <para>
/// El resultado se guarda en una caché en memoria: al volver a pintar el historial o
/// al repetir «/ver» no se vuelve a descargar ni a descodificar la misma imagen.
/// </para>
/// </remarks>
public sealed class RenderizadorImagenes
{
    /// <summary>Número máximo de imágenes que se conservan descargadas.</summary>
    private const int MaximoEnCache = 16;

    private readonly ClienteApi _api;
    private readonly OpcionesCliente _opciones;
    private readonly Dictionary<Guid, byte[]> _cache = [];
    private readonly Queue<Guid> _ordenLlegada = new();
    private readonly SemaphoreSlim _cerrojo = new(1, 1);

    /// <summary>Crea el renderizador.</summary>
    /// <param name="api">Cliente de la API, usado para descargar el contenido.</param>
    /// <param name="opciones">Configuración del cliente.</param>
    public RenderizadorImagenes(ClienteApi api, IOptions<OpcionesCliente> opciones)
    {
        ArgumentNullException.ThrowIfNull(opciones);

        _api = api;
        _opciones = opciones.Value;
    }

    /// <summary>Indica si la configuración pide dibujar las imágenes al recibirlas.</summary>
    public bool DibujaAlRecibir => _opciones.MostrarImagenesEnLinea && !Console.IsOutputRedirected;

    /// <summary>
    /// Descarga la imagen si hace falta y la dibuja. Cualquier fallo se comunica como
    /// un aviso: no poder ver una foto no debe interrumpir la conversación.
    /// </summary>
    /// <param name="adjunto">Adjunto anunciado en el mensaje.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    public async Task DibujarAsync(AdjuntoDto adjunto, CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(adjunto);

        if (Console.IsOutputRedirected)
        {
            // Con la salida redirigida no hay terminal que colorear: la ficha del
            // adjunto, que ya se ha impreso, es toda la información útil.
            return;
        }

        try
        {
            var datos = await ObtenerAsync(adjunto.Id, cancelacion).ConfigureAwait(false);
            AnsiConsole.Write(Componer(datos, adjunto));
        }
        catch (Exception excepcion) when (excepcion is not OperationCanceledException)
        {
            Presentacion.Aviso($"No se pudo mostrar '{adjunto.NombreArchivo}': {excepcion.Message}");
        }
    }

    /// <summary>Devuelve el contenido del adjunto, descargándolo solo la primera vez.</summary>
    /// <param name="adjuntoId">Adjunto solicitado.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    private async Task<byte[]> ObtenerAsync(Guid adjuntoId, CancellationToken cancelacion)
    {
        await _cerrojo.WaitAsync(cancelacion).ConfigureAwait(false);
        try
        {
            if (_cache.TryGetValue(adjuntoId, out var guardado))
            {
                return guardado;
            }

            var datos = await _api.DescargarAdjuntoAsync(adjuntoId, cancelacion).ConfigureAwait(false);

            _cache[adjuntoId] = datos;
            _ordenLlegada.Enqueue(adjuntoId);

            // Caché de tamaño fijo y de descarte por antigüedad: en una conversación
            // larga, las imágenes viejas ya no se van a repintar.
            while (_ordenLlegada.Count > MaximoEnCache)
            {
                _cache.Remove(_ordenLlegada.Dequeue());
            }

            return datos;
        }
        finally
        {
            _cerrojo.Release();
        }
    }

    /// <summary>Construye el lienzo con la imagen ajustada al ancho disponible.</summary>
    /// <param name="datos">Bytes de la imagen.</param>
    /// <param name="adjunto">Metadatos del adjunto.</param>
    private IRenderable Componer(byte[] datos, AdjuntoDto adjunto)
    {
        var imagen = new CanvasImage(datos)
        {
            MaxWidth = CalcularAnchura()
        };

        return new Panel(imagen)
        {
            Header = new PanelHeader($"[grey]{Presentacion.Escapar(adjunto.NombreArchivo)}[/]"),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Grey37),
            Padding = new Padding(0, 0, 0, 0)
        };
    }

    /// <summary>
    /// Calcula la anchura en columnas: la configurada, recortada a lo que quepa en la
    /// ventana descontando el marco del panel.
    /// </summary>
    private int CalcularAnchura()
    {
        var disponible = Math.Max(AnsiConsole.Profile.Width - 4, 8);
        return Math.Min(_opciones.ColumnasImagen, disponible);
    }
}
