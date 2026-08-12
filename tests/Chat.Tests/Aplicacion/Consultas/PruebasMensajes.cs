using System.Text;
using Chat.Aplicacion.Abstracciones;
using Chat.Aplicacion.Consultas.Mensajes;
using Chat.Dominio.Abstracciones;
using Chat.Dominio.Entidades;
using Chat.Dominio.Excepciones;
using Chat.Tests.Comun;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Chat.Tests.Aplicacion.Consultas;

/// <summary>Pruebas de la lectura del historial de una sala.</summary>
public sealed class PruebasObtenerMensajes
{
    private readonly IRepositorioMensajes _mensajes = Substitute.For<IRepositorioMensajes>();
    private readonly IRepositorioSalas _salas = Substitute.For<IRepositorioSalas>();
    private readonly ICifradorMensajes _cifrador = Substitute.For<ICifradorMensajes>();

    private readonly Sala _sala = Datos.Sala();
    private readonly Usuario _autor = Datos.Usuario();

    public PruebasObtenerMensajes()
    {
        _salas.ObtenerPorIdAsync(_sala.Id, Arg.Any<CancellationToken>()).Returns(_sala);
        _salas.EsMiembroAsync(_sala.Id, _autor.Id, Arg.Any<CancellationToken>()).Returns(true);

        _cifrador
            .IntentarDescifrar(Arg.Any<string>(), out Arg.Any<string?>())
            .Returns(llamada =>
            {
                llamada[1] = $"claro({(string)llamada[0]!})";
                return true;
            });
    }

    private ManejadorObtenerMensajes Manejador => new(
        _mensajes,
        _salas,
        _cifrador,
        NullLogger<ManejadorObtenerMensajes>.Instance);

    [Fact]
    public async Task ElHistorialLlegaDescifradoYConSuAutor()
    {
        var mensaje = Datos.Mensaje(_sala.Id, _autor.Id, "abc");
        mensaje.Usuario = _autor;
        Devolver(mensaje);

        var resultado = await Manejador.ManejarAsync(new ConsultaObtenerMensajes(_sala.Id, _autor.Id));

        var unico = Assert.Single(resultado);
        Assert.Equal("claro(abc)", unico.Texto);
        Assert.Equal("ana", unico.NombreUsuario);
        Assert.Equal("General", unico.SalaNombre);
    }

    [Fact]
    public async Task UnMensajeDeSoloImagenLlegaConTextoVacio()
    {
        var adjunto = Datos.Adjunto(_sala.Id, _autor.Id);
        var mensaje = Datos.Mensaje(_sala.Id, _autor.Id, textoCifrado: null, adjuntoId: adjunto.Id);
        mensaje.Usuario = _autor;
        mensaje.Adjunto = adjunto;
        Devolver(mensaje);

        var unico = Assert.Single(await Manejador.ManejarAsync(new ConsultaObtenerMensajes(_sala.Id, _autor.Id)));

        Assert.Equal(string.Empty, unico.Texto);
        Assert.NotNull(unico.Adjunto);
        Assert.Equal("foto.png", unico.Adjunto.NombreArchivo);
        _cifrador.DidNotReceiveWithAnyArgs().IntentarDescifrar(default!, out _);
    }

    [Fact]
    public async Task UnMensajeIlegibleNoTumbaLaConsultaEntera()
    {
        // Una clave rotada o un dato corrupto dejan un hueco marcado, pero el resto de
        // la conversación se sigue pudiendo leer.
        _cifrador
            .IntentarDescifrar("roto", out Arg.Any<string?>())
            .Returns(llamada =>
            {
                llamada[1] = null;
                return false;
            });

        var bueno = Datos.Mensaje(_sala.Id, _autor.Id, "abc");
        var roto = Datos.Mensaje(_sala.Id, _autor.Id, "roto");
        Devolver(bueno, roto);

        var resultado = await Manejador.ManejarAsync(new ConsultaObtenerMensajes(_sala.Id, _autor.Id));

        Assert.Equal(2, resultado.Count);
        Assert.Equal("claro(abc)", resultado[0].Texto);
        Assert.Contains("ilegible", resultado[1].Texto, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnAutorBorradoSeMuestraComoDesconocido()
    {
        Devolver(Datos.Mensaje(_sala.Id, _autor.Id, "abc"));

        var unico = Assert.Single(await Manejador.ManejarAsync(new ConsultaObtenerMensajes(_sala.Id, _autor.Id)));

        Assert.Equal("(desconocido)", unico.NombreUsuario);
    }

    [Fact]
    public async Task SoloLosMiembrosPuedenLeerElHistorial()
    {
        var intruso = Guid.CreateVersion7();
        _salas.EsMiembroAsync(_sala.Id, intruso, Arg.Any<CancellationToken>()).Returns(false);

        await Assert.ThrowsAsync<ExcepcionAutorizacion>(
            () => Manejador.ManejarAsync(new ConsultaObtenerMensajes(_sala.Id, intruso)));
    }

    [Fact]
    public async Task UnAdministradorPuedeAuditarSinSerMiembro()
    {
        var auditor = Guid.CreateVersion7();
        _salas.EsMiembroAsync(_sala.Id, auditor, Arg.Any<CancellationToken>()).Returns(false);
        Devolver(Datos.Mensaje(_sala.Id, _autor.Id, "abc"));

        var resultado = await Manejador.ManejarAsync(
            new ConsultaObtenerMensajes(_sala.Id, auditor, OmitirComprobacionMembresia: true));

        Assert.Single(resultado);
    }

    [Fact]
    public async Task UnaSalaInexistenteSeRechaza()
    {
        var salaId = Guid.CreateVersion7();
        _salas.ObtenerPorIdAsync(salaId, Arg.Any<CancellationToken>()).Returns((Sala?)null);

        await Assert.ThrowsAsync<ExcepcionNoEncontrado>(
            () => Manejador.ManejarAsync(new ConsultaObtenerMensajes(salaId, _autor.Id)));
    }

    [Theory]
    [InlineData(0, 50)]
    [InlineData(-1, 50)]
    [InlineData(10, 10)]
    [InlineData(5000, ManejadorObtenerMensajes.CantidadMaxima)]
    public async Task LaCantidadPedidaSeAjustaAlRangoPermitido(int pedida, int esperada)
    {
        Devolver();

        await Manejador.ManejarAsync(new ConsultaObtenerMensajes(_sala.Id, _autor.Id, pedida));

        await _mensajes.Received(1).ObtenerRecientesAsync(
            _sala.Id, esperada, Arg.Any<DateTimeOffset?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LaPaginacionHaciaAtrasLlegaAlRepositorio()
    {
        var limite = Datos.Ahora.AddHours(-1);
        Devolver();

        await Manejador.ManejarAsync(new ConsultaObtenerMensajes(_sala.Id, _autor.Id, 50, limite));

        await _mensajes.Received(1).ObtenerRecientesAsync(
            _sala.Id, 50, limite, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EnUnaDirectaElNombreDeSalaEsElDelAutorDeCadaMensaje()
    {
        var directa = Datos.Sala(tipo: TipoSala.Directa, nombre: "directa:x:y", claveDirecta: "x:y");
        _salas.ObtenerPorIdAsync(directa.Id, Arg.Any<CancellationToken>()).Returns(directa);
        _salas.EsMiembroAsync(directa.Id, _autor.Id, Arg.Any<CancellationToken>()).Returns(true);

        var mensaje = Datos.Mensaje(directa.Id, _autor.Id, "abc");
        mensaje.Usuario = _autor;

        _mensajes
            .ObtenerRecientesAsync(directa.Id, Arg.Any<int>(), Arg.Any<DateTimeOffset?>(), Arg.Any<CancellationToken>())
            .Returns([mensaje]);

        var unico = Assert.Single(await Manejador.ManejarAsync(new ConsultaObtenerMensajes(directa.Id, _autor.Id)));

        Assert.Equal("ana", unico.SalaNombre);
    }

    [Fact]
    public async Task UnaConsultaNulaSeRechaza()
        => await Assert.ThrowsAsync<ArgumentNullException>(() => Manejador.ManejarAsync(null!));

    /// <summary>Programa el historial que devolverá el repositorio.</summary>
    /// <param name="mensajes">Mensajes en orden cronológico.</param>
    private void Devolver(params Mensaje[] mensajes)
        => _mensajes
            .ObtenerRecientesAsync(_sala.Id, Arg.Any<int>(), Arg.Any<DateTimeOffset?>(), Arg.Any<CancellationToken>())
            .Returns(mensajes);
}

/// <summary>Pruebas de la descarga de adjuntos.</summary>
public sealed class PruebasDescargarAdjunto : IDisposable
{
    private readonly IRepositorioAdjuntos _adjuntos = Substitute.For<IRepositorioAdjuntos>();
    private readonly IRepositorioSalas _salas = Substitute.For<IRepositorioSalas>();
    private readonly AlmacenObjetosDePrueba _almacen = new();
    private readonly Chat.Infraestructura.Seguridad.ServicioCifradorMensajes _cifrador =
        new(Opciones.De(Opciones.Cifrado()));

    private readonly Guid _salaId = Guid.CreateVersion7();
    private readonly Guid _usuarioId = Guid.CreateVersion7();

    /// <inheritdoc />
    public void Dispose() => _cifrador.Dispose();

    private ManejadorDescargarAdjunto Manejador => new(_adjuntos, _salas, _almacen, _cifrador);

    [Fact]
    public async Task UnMiembroRecuperaElContenidoEnClaro()
    {
        var contenido = Encoding.UTF8.GetBytes("contenido del adjunto");
        var adjunto = await PrepararAsync(contenido);

        await using var resultado = await Manejador.ManejarAsync(
            new ConsultaDescargarAdjunto(adjunto.Id, _usuarioId));

        Assert.Equal(adjunto.TipoMime, resultado.TipoMime);
        Assert.Equal(adjunto.NombreArchivo, resultado.NombreArchivo);
        Assert.Equal(adjunto.TamanoBytes, resultado.TamanoBytes);

        using var descargado = new MemoryStream();
        await resultado.Contenido.CopyToAsync(descargado);
        Assert.Equal(contenido, descargado.ToArray());
    }

    [Fact]
    public async Task QuienNoEsMiembroDeLaSalaNoPuedeDescargar()
    {
        // La autorización va por la sala del adjunto, no por quién lo subió.
        var adjunto = await PrepararAsync([1, 2, 3], esMiembro: false);

        await Assert.ThrowsAsync<ExcepcionAutorizacion>(
            () => Manejador.ManejarAsync(new ConsultaDescargarAdjunto(adjunto.Id, _usuarioId)));
    }

    [Fact]
    public async Task UnAdministradorPuedeDescargarSinSerMiembro()
    {
        var adjunto = await PrepararAsync([1, 2, 3], esMiembro: false);

        await using var resultado = await Manejador.ManejarAsync(
            new ConsultaDescargarAdjunto(adjunto.Id, _usuarioId, OmitirComprobacionMembresia: true));

        Assert.NotNull(resultado.Contenido);
    }

    [Fact]
    public async Task UnAdjuntoInexistenteSeRechaza()
    {
        var adjuntoId = Guid.CreateVersion7();
        _adjuntos.ObtenerPorIdAsync(adjuntoId, Arg.Any<CancellationToken>()).Returns((Adjunto?)null);

        await Assert.ThrowsAsync<ExcepcionNoEncontrado>(
            () => Manejador.ManejarAsync(new ConsultaDescargarAdjunto(adjuntoId, _usuarioId)));
    }

    [Fact]
    public async Task UnIdentificadorVacioSeRechaza()
        => await Assert.ThrowsAsync<ExcepcionValidacion>(
            () => Manejador.ManejarAsync(new ConsultaDescargarAdjunto(Guid.Empty, _usuarioId)));

    [Fact]
    public async Task UnContenidoManipuladoFallaAlLeerse()
    {
        // La integridad se comprueba marco a marco: la manipulación se detecta durante
        // la lectura, no después de haber entregado el fichero entero.
        var adjunto = await PrepararAsync(Encoding.UTF8.GetBytes("contenido íntegro"));

        var guardado = _almacen.Contenido(adjunto.ClaveObjeto);
        guardado[^1] ^= 0xFF;
        await _almacen.GuardarAsync(adjunto.ClaveObjeto, new MemoryStream(guardado), guardado.Length, "x");

        await using var resultado = await Manejador.ManejarAsync(
            new ConsultaDescargarAdjunto(adjunto.Id, _usuarioId));

        using var destino = new MemoryStream();
        await Assert.ThrowsAsync<System.Security.Cryptography.CryptographicException>(
            () => resultado.Contenido.CopyToAsync(destino));
    }

    /// <summary>Deja un adjunto registrado y su contenido cifrado en el almacén.</summary>
    /// <param name="contenido">Contenido en claro.</param>
    /// <param name="esMiembro">Indica si quien consulta pertenece a la sala.</param>
    private async Task<Adjunto> PrepararAsync(byte[] contenido, bool esMiembro = true)
    {
        var adjunto = Datos.Adjunto(_salaId, _usuarioId);
        adjunto.TamanoBytes = contenido.Length;

        _adjuntos.ObtenerPorIdAsync(adjunto.Id, Arg.Any<CancellationToken>()).Returns(adjunto);
        _salas.EsMiembroAsync(_salaId, _usuarioId, Arg.Any<CancellationToken>()).Returns(esMiembro);

        await using var claro = new MemoryStream(contenido, writable: false);
        await using var cifrado = _cifrador.Cifrar(claro);
        await _almacen.GuardarAsync(adjunto.ClaveObjeto, cifrado, 0, adjunto.TipoMime);

        return adjunto;
    }
}
