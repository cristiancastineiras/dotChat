using Chat.Aplicacion.Abstracciones;
using Chat.Aplicacion.Opciones;
using Chat.Dominio.Excepciones;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Chat.Infraestructura.Imagenes;

/// <summary>Implementación de <see cref="IProcesadorImagenes"/> sobre ImageSharp.</summary>
/// <remarks>
/// El proceso es siempre el mismo y en este orden: leer la cabecera para conocer el
/// tamaño sin descodificar nada, rechazar lo que exceda los límites, descodificar un
/// solo fotograma, reescalar si hace falta, tirar los perfiles de metadatos y volver a
/// codificar. Lo que se persiste es el resultado de ese último paso.
/// </remarks>
public sealed class ProcesadorImagenesImageSharp : IProcesadorImagenes
{
    /// <summary>Tipo MIME de salida para las imágenes que conservan transparencia.</summary>
    private const string MimePng = "image/png";

    /// <summary>Tipo MIME de salida para las imágenes opacas.</summary>
    private const string MimeJpeg = "image/jpeg";

    private readonly AdjuntosOptions _opciones;

    /// <summary>Crea el procesador.</summary>
    /// <param name="opciones">Límites configurados para los adjuntos.</param>
    public ProcesadorImagenesImageSharp(IOptions<AdjuntosOptions> opciones)
    {
        ArgumentNullException.ThrowIfNull(opciones);
        _opciones = opciones.Value;
    }

    /// <inheritdoc />
    public async Task<bool> EsImagenAsync(Stream origen, CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(origen);

        if (!origen.CanSeek)
        {
            // Sin poder rebobinar, mirar la cabecera consumiría el contenido que luego
            // hay que almacenar. Se trata como archivo cualquiera, que es lo seguro.
            return false;
        }

        var posicion = origen.Position;

        try
        {
            var informacion = await Image
                .IdentifyAsync(ConstruirOpciones(), origen, cancelacion)
                .ConfigureAwait(false);

            return EsFormatoAdmitido(informacion.Metadata.DecodedImageFormat);
        }
        catch (Exception excepcion) when (excepcion is UnknownImageFormatException or InvalidImageContentException)
        {
            return false;
        }
        finally
        {
            origen.Position = posicion;
        }
    }

    /// <inheritdoc />
    public async Task<ImagenNormalizada> NormalizarAsync(Stream origen, CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(origen);

        if (origen.CanSeek && origen.Length > _opciones.TamanoMaximoImagenBytes)
        {
            throw new ExcepcionValidacion(
                "archivo",
                $"Una imagen no puede superar {_opciones.TamanoMaximoImagenBytes / 1024 / 1024} MiB.");
        }

        var opcionesDescodificacion = ConstruirOpciones();
        var informacion = await LeerCabeceraAsync(opcionesDescodificacion, origen, cancelacion).ConfigureAwait(false);
        var formatoOrigen = informacion.Metadata.DecodedImageFormat;

        ValidarFormato(formatoOrigen);
        ValidarSuperficie(informacion.Width, informacion.Height);

        if (origen.CanSeek)
        {
            origen.Position = 0;
        }

        using var imagen = await Image
            .LoadAsync<Rgba32>(opcionesDescodificacion, origen, cancelacion)
            .ConfigureAwait(false);

        Reescalar(imagen);

        // Los perfiles arrastran geolocalización, número de serie de la cámara y
        // marcas de edición. No se reenvían: se descartan antes de codificar.
        imagen.Metadata.ExifProfile = null;
        imagen.Metadata.IptcProfile = null;
        imagen.Metadata.XmpProfile = null;

        return await CodificarAsync(imagen, formatoOrigen, cancelacion).ConfigureAwait(false);
    }

    /// <summary>
    /// Opciones de descodificación comunes: un solo fotograma, porque un GIF animado
    /// de miles de cuadros ocupa poco en disco y muchísimo en memoria, y sin metadatos.
    /// </summary>
    private static DecoderOptions ConstruirOpciones() => new()
    {
        MaxFrames = 1,
        SkipMetadata = true
    };

    /// <summary>Lee las dimensiones y el formato sin llegar a descodificar los píxeles.</summary>
    /// <param name="opciones">Opciones de descodificación.</param>
    /// <param name="origen">Flujo con el contenido.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    /// <exception cref="ExcepcionValidacion">Si el contenido no es una imagen reconocible.</exception>
    private static async Task<ImageInfo> LeerCabeceraAsync(
        DecoderOptions opciones,
        Stream origen,
        CancellationToken cancelacion)
    {
        var posicion = origen.CanSeek ? origen.Position : 0;

        try
        {
            return await Image.IdentifyAsync(opciones, origen, cancelacion).ConfigureAwait(false);
        }
        catch (Exception excepcion) when (excepcion is UnknownImageFormatException or InvalidImageContentException)
        {
            throw new ExcepcionValidacion(
                "archivo",
                "El fichero no es una imagen válida. Se admiten PNG, JPEG, GIF, WebP y BMP.");
        }
        finally
        {
            if (origen.CanSeek)
            {
                origen.Position = posicion;
            }
        }
    }

    /// <summary>Indica si el formato detectado está entre los que se saben normalizar.</summary>
    /// <param name="formato">Formato detectado al leer la cabecera.</param>
    private static bool EsFormatoAdmitido(IImageFormat? formato)
        => formato is PngFormat or JpegFormat or GifFormat or WebpFormat or BmpFormat;

    /// <summary>Comprueba que el formato de origen está entre los admitidos.</summary>
    /// <param name="formato">Formato detectado al leer la cabecera.</param>
    /// <exception cref="ExcepcionValidacion">Si el formato no está permitido.</exception>
    private static void ValidarFormato(IImageFormat? formato)
    {
        if (!EsFormatoAdmitido(formato))
        {
            throw new ExcepcionValidacion(
                "archivo",
                $"El formato '{formato?.Name ?? "desconocido"}' no está admitido. Use PNG, JPEG, GIF, WebP o BMP.");
        }
    }

    /// <summary>Rechaza las imágenes cuya descodificación reservaría demasiada memoria.</summary>
    /// <param name="ancho">Anchura declarada en la cabecera.</param>
    /// <param name="alto">Altura declarada en la cabecera.</param>
    /// <exception cref="ExcepcionValidacion">Si la superficie supera el límite configurado.</exception>
    private void ValidarSuperficie(int ancho, int alto)
    {
        if (ancho <= 0 || alto <= 0)
        {
            throw new ExcepcionValidacion("archivo", "La imagen no declara unas dimensiones válidas.");
        }

        if ((long)ancho * alto > _opciones.PixelesMaximos())
        {
            throw new ExcepcionValidacion(
                "archivo",
                $"La imagen es demasiado grande ({ancho}×{alto}). El máximo es de {_opciones.MegapixelesMaximos} megapíxeles.");
        }
    }

    /// <summary>Reduce la imagen si excede el lado máximo, conservando la proporción.</summary>
    /// <param name="imagen">Imagen ya descodificada.</param>
    private void Reescalar(Image<Rgba32> imagen)
    {
        var lado = _opciones.LadoMaximoPixeles;

        if (imagen.Width <= lado && imagen.Height <= lado)
        {
            return;
        }

        imagen.Mutate(operaciones => operaciones.Resize(new ResizeOptions
        {
            Size = new Size(lado, lado),
            Mode = ResizeMode.Max,
            Sampler = KnownResamplers.Lanczos3
        }));
    }

    /// <summary>
    /// Codifica la imagen normalizada. Se elige PNG cuando el origen podía llevar
    /// transparencia —convertir esos casos a JPEG pinta el fondo de negro— y JPEG en
    /// el resto, que es donde compensa la compresión con pérdida.
    /// </summary>
    /// <param name="imagen">Imagen procesada.</param>
    /// <param name="formatoOrigen">Formato del que provenía.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    private async Task<ImagenNormalizada> CodificarAsync(
        Image<Rgba32> imagen,
        IImageFormat? formatoOrigen,
        CancellationToken cancelacion)
    {
        var conservarAlfa = formatoOrigen is PngFormat or GifFormat or WebpFormat;

        using var salida = new MemoryStream();

        if (conservarAlfa)
        {
            await imagen
                .SaveAsPngAsync(salida, new PngEncoder { CompressionLevel = PngCompressionLevel.BestCompression }, cancelacion)
                .ConfigureAwait(false);

            return new ImagenNormalizada(salida.ToArray(), MimePng, ".png", imagen.Width, imagen.Height);
        }

        await imagen
            .SaveAsJpegAsync(salida, new JpegEncoder { Quality = _opciones.CalidadJpeg }, cancelacion)
            .ConfigureAwait(false);

        return new ImagenNormalizada(salida.ToArray(), MimeJpeg, ".jpg", imagen.Width, imagen.Height);
    }
}
