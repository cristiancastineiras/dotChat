using Chat.Aplicacion.Opciones;
using Chat.Dominio.Excepciones;
using Chat.Infraestructura.Imagenes;
using Chat.Tests.Comun;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;

namespace Chat.Tests.Infraestructura;

/// <summary>
/// Pruebas del tratamiento de imágenes. Es la puerta por la que entra contenido
/// arbitrario en el servidor, así que se comprueba lo que de verdad la justifica: que
/// el formato se determina descodificando y no por el nombre, que los metadatos del
/// remitente no se reenvían y que ni una imagen enorme ni una «bomba» de descompresión
/// llegan a reservar memoria.
/// </summary>
public sealed class PruebasProcesadorImagenes
{
    private ProcesadorImagenesImageSharp Procesador(
        int ladoMaximo = 1600,
        int megapixelesMaximos = 40,
        long tamanoMaximoImagen = 8L * 1024 * 1024)
        => new(Opciones.De(new AdjuntosOptions
        {
            LadoMaximoPixeles = ladoMaximo,
            MegapixelesMaximos = megapixelesMaximos,
            TamanoMaximoImagenBytes = tamanoMaximoImagen,
            CalidadJpeg = 82
        }));

    [Fact]
    public async Task UnPngSeReconoceComoImagen()
    {
        await using var origen = Png(64, 64);

        Assert.True(await Procesador().EsImagenAsync(origen));
    }

    [Fact]
    public async Task ComprobarSiEsImagenDevuelveElFlujoDondeEstaba()
    {
        // Si consumiera el contenido, lo que se almacenase después iría truncado.
        await using var origen = Png(32, 32);
        origen.Position = 0;

        await Procesador().EsImagenAsync(origen);

        Assert.Equal(0, origen.Position);
    }

    [Fact]
    public async Task UnFicheroQueNoEsUnaImagenNoSeReconoceComoTal()
    {
        await using var origen = new MemoryStream("esto es un documento de texto"u8.ToArray());

        Assert.False(await Procesador().EsImagenAsync(origen));
    }

    [Fact]
    public async Task UnFlujoSinBusquedaSeTrataComoArchivoCualquiera()
    {
        // Sin poder rebobinar, mirar la cabecera consumiría lo que hay que almacenar.
        await using var origen = new FlujoSinBusqueda(Png(32, 32).ToArray());

        Assert.False(await Procesador().EsImagenAsync(origen));
    }

    [Fact]
    public async Task UnPngSeNormalizaAPngParaNoPerderLaTransparencia()
    {
        await using var origen = Png(100, 50);

        var imagen = await Procesador().NormalizarAsync(origen);

        Assert.Equal("image/png", imagen.TipoMime);
        Assert.Equal(".png", imagen.Extension);
        Assert.Equal(100, imagen.Ancho);
        Assert.Equal(50, imagen.Alto);
    }

    [Fact]
    public async Task UnJpegSeNormalizaAJpeg()
    {
        await using var origen = Jpeg(120, 80);

        var imagen = await Procesador().NormalizarAsync(origen);

        Assert.Equal("image/jpeg", imagen.TipoMime);
        Assert.Equal(".jpg", imagen.Extension);
    }

    [Theory]
    [InlineData("gif")]
    [InlineData("bmp")]
    public async Task ElRestoDeFormatosAdmitidosTambienSeNormaliza(string formato)
    {
        await using var origen = EnFormato(formato, 40, 40);

        var imagen = await Procesador().NormalizarAsync(origen);

        // Un GIF puede llevar transparencia y un BMP no: de ahí que uno salga en PNG
        // y el otro en JPEG.
        Assert.Equal(formato == "gif" ? "image/png" : "image/jpeg", imagen.TipoMime);
        Assert.NotEmpty(imagen.Datos);
    }

    [Fact]
    public async Task LoQueSeGuardaEsSiempreElResultadoDeRecodificar()
    {
        // Nunca se persiste el fichero original: así no se almacena nada que no sea una
        // imagen legítima descodificada por el servidor.
        var original = Png(60, 60).ToArray();
        await using var origen = new MemoryStream(original, writable: false);

        var imagen = await Procesador().NormalizarAsync(origen);

        Assert.NotEqual(original, imagen.Datos);
        using var releida = Image.Load(imagen.Datos);
        Assert.Equal(60, releida.Width);
    }

    [Fact]
    public async Task LosMetadatosDelRemitenteNoSeReenvian()
    {
        // Un EXIF arrastra geolocalización y modelo de cámara que quien comparte la
        // foto probablemente no sabe que está mandando.
        using var conExif = new Image<Rgba32>(50, 50);
        conExif.Metadata.ExifProfile = new ExifProfile();
        conExif.Metadata.ExifProfile.SetValue(ExifTag.Copyright, "Ana");
        conExif.Metadata.ExifProfile.SetValue(ExifTag.Software, "camara-secreta");

        await using var origen = new MemoryStream();
        await conExif.SaveAsJpegAsync(origen);
        origen.Position = 0;

        var imagen = await Procesador().NormalizarAsync(origen);

        using var resultado = Image.Load(imagen.Datos);
        Assert.Null(resultado.Metadata.ExifProfile);
        Assert.Null(resultado.Metadata.IptcProfile);
        Assert.Null(resultado.Metadata.XmpProfile);
    }

    [Fact]
    public async Task UnaImagenGrandeSeReescalaConservandoLaProporcion()
    {
        await using var origen = Png(800, 400);

        var imagen = await Procesador(ladoMaximo: 200).NormalizarAsync(origen);

        Assert.Equal(200, imagen.Ancho);
        Assert.Equal(100, imagen.Alto);
    }

    [Fact]
    public async Task UnaImagenPequenaNoSeAmplia()
    {
        await using var origen = Png(80, 60);

        var imagen = await Procesador(ladoMaximo: 1600).NormalizarAsync(origen);

        Assert.Equal(80, imagen.Ancho);
        Assert.Equal(60, imagen.Alto);
    }

    [Fact]
    public async Task UnaImagenQueSupereElTamanoMaximoSeRechaza()
    {
        await using var origen = Png(200, 200);

        // El límite se fija justo por debajo de lo que ocupa: así la prueba no depende
        // de cuánto llegue a comprimir el codificador.
        var excepcion = await Assert.ThrowsAsync<ExcepcionValidacion>(
            () => Procesador(tamanoMaximoImagen: origen.Length - 1).NormalizarAsync(origen));

        Assert.Contains("archivo", excepcion.Errores.Keys);
    }

    [Fact]
    public async Task UnaSuperficieDesmesuradaSeRechazaLeyendoSoloLaCabecera()
    {
        // Es la defensa contra las «bombas de descompresión»: un fichero diminuto que
        // se expande a gigabytes al descodificarlo. Se corta antes de reservar memoria.
        await using var origen = Png(2000, 2000);

        var excepcion = await Assert.ThrowsAsync<ExcepcionValidacion>(
            () => Procesador(megapixelesMaximos: 1).NormalizarAsync(origen));

        Assert.Contains("demasiado grande", excepcion.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnContenidoQueNoEsUnaImagenSeRechazaAlNormalizar()
    {
        await using var origen = new MemoryStream("PK esto es un zip"u8.ToArray());

        var excepcion = await Assert.ThrowsAsync<ExcepcionValidacion>(
            () => Procesador().NormalizarAsync(origen));

        Assert.Contains("no es una imagen válida", excepcion.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ElNombreDelFicheroNoDecideNadaSobreElFormato()
    {
        // El formato se determina descodificando: que algo se llame «.png» no significa
        // nada, y aquí un JPEG entra por lo que es y sale como JPEG.
        await using var origen = Jpeg(30, 30);

        Assert.Equal("image/jpeg", (await Procesador().NormalizarAsync(origen)).TipoMime);
    }

    [Fact]
    public async Task LosFlujosNulosSeRechazan()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => Procesador().EsImagenAsync(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => Procesador().NormalizarAsync(null!));
    }

    [Fact]
    public void SinOpcionesNoSePuedeConstruir()
        => Assert.Throws<ArgumentNullException>(() => new ProcesadorImagenesImageSharp(null!));

    /// <summary>Genera un PNG de las dimensiones indicadas.</summary>
    /// <param name="ancho">Anchura en píxeles.</param>
    /// <param name="alto">Altura en píxeles.</param>
    private static MemoryStream Png(int ancho, int alto) => EnFormato("png", ancho, alto);

    /// <summary>Genera un JPEG de las dimensiones indicadas.</summary>
    /// <param name="ancho">Anchura en píxeles.</param>
    /// <param name="alto">Altura en píxeles.</param>
    private static MemoryStream Jpeg(int ancho, int alto) => EnFormato("jpeg", ancho, alto);

    /// <summary>Genera una imagen sintética en el formato pedido.</summary>
    /// <param name="formato">Formato de salida.</param>
    /// <param name="ancho">Anchura en píxeles.</param>
    /// <param name="alto">Altura en píxeles.</param>
    private static MemoryStream EnFormato(string formato, int ancho, int alto)
    {
        using var imagen = new Image<Rgba32>(ancho, alto);

        for (var y = 0; y < alto; y++)
        {
            for (var x = 0; x < ancho; x++)
            {
                imagen[x, y] = new Rgba32((byte)(x % 256), (byte)(y % 256), 128, 255);
            }
        }

        var salida = new MemoryStream();

        switch (formato)
        {
            case "png":
                imagen.SaveAsPng(salida, new PngEncoder());
                break;
            case "jpeg":
                imagen.SaveAsJpeg(salida, new JpegEncoder());
                break;
            case "gif":
                imagen.SaveAsGif(salida, new GifEncoder());
                break;
            default:
                imagen.SaveAsBmp(salida, new BmpEncoder());
                break;
        }

        salida.Position = 0;
        return salida;
    }

    /// <summary>Flujo de solo lectura sin búsqueda, como el de una petición en curso.</summary>
    /// <param name="contenido">Contenido que entrega.</param>
    private sealed class FlujoSinBusqueda(byte[] contenido) : MemoryStream(contenido, writable: false)
    {
        /// <inheritdoc />
        public override bool CanSeek => false;
    }
}
