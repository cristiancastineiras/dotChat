using System.Security.Cryptography;
using System.Text;
using Chat.Aplicacion.Abstracciones;
using Chat.Aplicacion.Comandos.Mensajes;
using Chat.Dominio.Abstracciones;
using Chat.Dominio.Entidades;
using Chat.Dominio.Excepciones;
using Chat.Tests.Comun;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Chat.Tests.Aplicacion.Comandos;

/// <summary>
/// Pruebas de la subida de archivos. Se ejercita con el cifrador de flujo real, de
/// modo que lo que llega al almacén es un criptograma de verdad.
/// </summary>
public sealed class PruebasSubirAdjunto : IDisposable
{
    private readonly IRepositorioAdjuntos _adjuntos = Substitute.For<IRepositorioAdjuntos>();
    private readonly IRepositorioSalas _salas = Substitute.For<IRepositorioSalas>();
    private readonly IProcesadorImagenes _procesador = Substitute.For<IProcesadorImagenes>();
    private readonly IUnidadDeTrabajo _unidadDeTrabajo = Substitute.For<IUnidadDeTrabajo>();
    private readonly AlmacenObjetosDePrueba _almacen = new();
    private readonly Chat.Infraestructura.Seguridad.ServicioCifradorMensajes _cifrador =
        new(Opciones.De(Opciones.Cifrado()));
    private readonly RelojFijo _reloj = new();

    private readonly Sala _sala = Datos.Sala();
    private readonly Guid _usuarioId = Guid.CreateVersion7();

    public PruebasSubirAdjunto()
    {
        _salas.ObtenerPorIdAsync(_sala.Id, Arg.Any<CancellationToken>()).Returns(_sala);
        _salas.EsMiembroAsync(_sala.Id, _usuarioId, Arg.Any<CancellationToken>()).Returns(true);
        _procesador.EsImagenAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>()).Returns(false);
    }

    /// <inheritdoc />
    public void Dispose() => _cifrador.Dispose();

    private ManejadorSubirAdjunto Manejador(bool activados = true, long tamanoMaximo = 25L * 1024 * 1024) => new(
        _adjuntos,
        _salas,
        _procesador,
        _cifrador,
        _almacen,
        _unidadDeTrabajo,
        _reloj,
        Opciones.De(Opciones.Adjuntos(activados, tamanoMaximo)),
        NullLogger<ManejadorSubirAdjunto>.Instance);

    private ComandoSubirAdjunto Comando(byte[] contenido, string nombre = "notas.txt")
        => new(_usuarioId, _sala.Id, nombre, new MemoryStream(contenido, writable: false), contenido.Length);

    [Fact]
    public async Task UnArchivoSeCifraAntesDeLlegarAlAlmacenYSeRegistraSuFicha()
    {
        var contenido = Encoding.UTF8.GetBytes("contenido del fichero");

        var dto = await Manejador().ManejarAsync(Comando(contenido));

        Assert.Equal("notas.txt", dto.NombreArchivo);
        Assert.Equal("text/plain", dto.TipoMime);
        Assert.Equal(TipoAdjunto.Archivo, dto.Tipo);
        Assert.Equal(contenido.Length, dto.TamanoBytes);
        Assert.Null(dto.Ancho);

        // Lo que se guarda no se parece al original: el almacén nunca ve el contenido.
        var clave = Adjunto.ConstruirClave(_sala.Id, dto.Id, _reloj.Ahora);
        var guardado = _almacen.Contenido(clave);
        Assert.NotEqual(contenido, guardado);
        Assert.True(guardado.Length > contenido.Length);

        await _unidadDeTrabajo.Received(1).GuardarCambiosAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LoAlmacenadoSeRecuperaIntactoAlDescifrarlo()
    {
        var contenido = Encoding.UTF8.GetBytes("el contenido tiene que volver entero");

        var dto = await Manejador().ManejarAsync(Comando(contenido));

        var clave = Adjunto.ConstruirClave(_sala.Id, dto.Id, _reloj.Ahora);
        await using var cifrado = new MemoryStream(_almacen.Contenido(clave), writable: false);
        await using var claro = _cifrador.Descifrar(cifrado);
        using var recuperado = new MemoryStream();
        await claro.CopyToAsync(recuperado);

        Assert.Equal(contenido, recuperado.ToArray());
    }

    [Fact]
    public async Task LaHuellaSeCalculaSobreElContenidoEnClaro()
    {
        // Es lo que permite comprobar en la descarga que lo que llega es lo que se subió.
        var contenido = Encoding.UTF8.GetBytes("contenido con huella");
        var esperada = Convert.ToHexStringLower(SHA256.HashData(contenido));

        await Manejador().ManejarAsync(Comando(contenido));

        await _adjuntos.Received(1).AgregarAsync(
            Arg.Is<Adjunto>(a => a.Huella == esperada),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UnaImagenSeNormalizaYSeGuardaConSuFormatoReal()
    {
        var normalizada = new ImagenNormalizada([1, 2, 3, 4, 5], "image/webp", ".webp", 800, 600);

        _procesador.EsImagenAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>()).Returns(true);
        _procesador.NormalizarAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>()).Returns(normalizada);

        var dto = await Manejador().ManejarAsync(Comando([9, 9, 9], nombre: "foto.png"));

        // La extensión se ajusta a lo que de verdad se almacenó, no a lo que traía.
        Assert.Equal("foto.webp", dto.NombreArchivo);
        Assert.Equal("image/webp", dto.TipoMime);
        Assert.Equal(TipoAdjunto.Imagen, dto.Tipo);
        Assert.Equal(800, dto.Ancho);
        Assert.Equal(600, dto.Alto);
        Assert.Equal(5, dto.TamanoBytes);
        Assert.True(dto.EsImagen);
    }

    [Fact]
    public async Task UnNombreSinExtensionRecibeLaDelFormatoAlmacenado()
    {
        _procesador.EsImagenAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>()).Returns(true);
        _procesador
            .NormalizarAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(new ImagenNormalizada([1], "image/png", ".png", 10, 10));

        var dto = await Manejador().ManejarAsync(Comando([9], nombre: "captura"));

        Assert.Equal("captura.png", dto.NombreArchivo);
    }

    [Fact]
    public async Task ElNombreRecibidoSeSaneaAntesDeUsarse()
    {
        var dto = await Manejador().ManejarAsync(Comando([1, 2, 3], nombre: @"..\..\etc\passwd"));

        Assert.Equal("passwd", dto.NombreArchivo);
    }

    [Fact]
    public async Task ConLosAdjuntosDesactivadosLaSubidaSeDeniega()
    {
        await Assert.ThrowsAsync<ExcepcionAutorizacion>(
            () => Manejador(activados: false).ManejarAsync(Comando([1])));

        Assert.Equal(0, _almacen.Total);
    }

    [Fact]
    public async Task NoSePuedeSubirAUnaSalaQueNoExiste()
    {
        _salas.ObtenerPorIdAsync(_sala.Id, Arg.Any<CancellationToken>()).Returns((Sala?)null);

        await Assert.ThrowsAsync<ExcepcionNoEncontrado>(() => Manejador().ManejarAsync(Comando([1])));
    }

    [Fact]
    public async Task NoSePuedeSubirAUnaSalaDeLaQueNoSeEsMiembro()
    {
        _salas.EsMiembroAsync(_sala.Id, _usuarioId, Arg.Any<CancellationToken>()).Returns(false);

        await Assert.ThrowsAsync<ExcepcionAutorizacion>(() => Manejador().ManejarAsync(Comando([1])));

        // Se comprueba antes de gastar un solo ciclo en el contenido.
        await _procesador.DidNotReceiveWithAnyArgs().EsImagenAsync(default!);
    }

    [Fact]
    public async Task UnFicheroVacioSeRechaza()
        => await Assert.ThrowsAsync<ExcepcionValidacion>(() => Manejador().ManejarAsync(Comando([])));

    [Fact]
    public async Task UnFicheroQueSupereElLimiteSeRechaza()
    {
        var excepcion = await Assert.ThrowsAsync<ExcepcionValidacion>(
            () => Manejador(tamanoMaximo: 8).ManejarAsync(Comando(new byte[16])));

        Assert.Contains("archivo", excepcion.Errores.Keys);
        Assert.Equal(0, _almacen.Total);
    }

    [Fact]
    public async Task SiLaFichaNoLlegaAGuardarseElObjetoSubidoSeRetira()
    {
        // Sin esta limpieza quedaría un objeto que nadie sabe que existe, ocupando
        // sitio en el almacén para siempre.
        _unidadDeTrabajo
            .GuardarCambiosAsync(Arg.Any<CancellationToken>())
            .Returns<Task<int>>(_ => throw new InvalidOperationException("la base de datos falló"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => Manejador().ManejarAsync(Comando([1, 2, 3])));

        Assert.Single(_almacen.Eliminados);
        Assert.Equal(0, _almacen.Total);
    }

    [Fact]
    public async Task UnFalloAlRetirarElObjetoNoTapaElErrorOriginal()
    {
        _unidadDeTrabajo
            .GuardarCambiosAsync(Arg.Any<CancellationToken>())
            .Returns<Task<int>>(_ => throw new InvalidOperationException("la base de datos falló"));

        var almacenRoto = Substitute.For<IAlmacenObjetos>();
        almacenRoto
            .EliminarAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new TimeoutException("el almacén tampoco responde"));

        var manejador = new ManejadorSubirAdjunto(
            _adjuntos,
            _salas,
            _procesador,
            _cifrador,
            almacenRoto,
            _unidadDeTrabajo,
            _reloj,
            Opciones.De(Opciones.Adjuntos()),
            NullLogger<ManejadorSubirAdjunto>.Instance);

        var excepcion = await Assert.ThrowsAsync<InvalidOperationException>(
            () => manejador.ManejarAsync(Comando([1, 2, 3])));

        Assert.Equal("la base de datos falló", excepcion.Message);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task LosIdentificadoresVaciosSeRechazan(bool usuarioVacio, bool salaVacia)
    {
        var comando = new ComandoSubirAdjunto(
            usuarioVacio ? Guid.Empty : _usuarioId,
            salaVacia ? Guid.Empty : _sala.Id,
            "notas.txt",
            new MemoryStream([1]),
            1);

        await Assert.ThrowsAsync<ExcepcionValidacion>(() => Manejador().ManejarAsync(comando));
    }

    [Fact]
    public async Task UnComandoNuloSeRechaza()
        => await Assert.ThrowsAsync<ArgumentNullException>(() => Manejador().ManejarAsync(null!));

    [Fact]
    public async Task UnFlujoSinBusquedaUsaElTamanoDeclaradoPorElCliente()
    {
        // Es el caso de una subida en tránsito: el flujo no sabe cuánto mide, y el
        // tamaño anunciado es lo único con lo que decidir si cabe.
        var contenido = Encoding.UTF8.GetBytes("contenido en transito");
        var comando = new ComandoSubirAdjunto(
            _usuarioId,
            _sala.Id,
            "notas.txt",
            new FlujoSinBusqueda(contenido),
            contenido.Length);

        var dto = await Manejador().ManejarAsync(comando);

        Assert.Equal(contenido.Length, dto.TamanoBytes);
    }

    /// <summary>Flujo de solo lectura que no admite búsqueda, como el de una petición HTTP en curso.</summary>
    /// <param name="contenido">Contenido que entrega.</param>
    private sealed class FlujoSinBusqueda(byte[] contenido) : MemoryStream(contenido, writable: false)
    {
        /// <inheritdoc />
        public override bool CanSeek => false;

        /// <inheritdoc />
        public override long Length => throw new NotSupportedException();
    }
}
