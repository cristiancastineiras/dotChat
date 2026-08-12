using Chat.Aplicacion.Abstracciones;
using Chat.Aplicacion.Comandos.Mensajes;
using Chat.Aplicacion.Dtos;
using Chat.Dominio.Abstracciones;
using Chat.Dominio.Entidades;
using Chat.Dominio.Excepciones;
using Chat.Tests.Comun;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Chat.Tests.Aplicacion.Comandos;

/// <summary>
/// Pruebas de la publicación de mensajes: es el camino caliente de la plataforma y
/// donde se concentran el cifrado, la autorización y la protección antirrepetición.
/// </summary>
public sealed class PruebasEnviarMensaje
{
    private readonly IRepositorioMensajes _mensajes = Substitute.For<IRepositorioMensajes>();
    private readonly IRepositorioSalas _salas = Substitute.For<IRepositorioSalas>();
    private readonly IRepositorioUsuarios _usuarios = Substitute.For<IRepositorioUsuarios>();
    private readonly IRepositorioAdjuntos _adjuntos = Substitute.For<IRepositorioAdjuntos>();
    private readonly ICifradorMensajes _cifrador = Substitute.For<ICifradorMensajes>();
    private readonly IProtectorRepeticion _protector = Substitute.For<IProtectorRepeticion>();
    private readonly INotificadorTiempoReal _notificador = Substitute.For<INotificadorTiempoReal>();
    private readonly IUnidadDeTrabajo _unidadDeTrabajo = Substitute.For<IUnidadDeTrabajo>();
    private readonly RelojFijo _reloj = new();

    private readonly Usuario _autor = Datos.Usuario();
    private readonly Sala _sala = Datos.Sala();

    public PruebasEnviarMensaje()
    {
        _protector.RegistrarSiEsNuevoAsync("mensaje", Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        _salas.ObtenerPorIdAsync(_sala.Id, Arg.Any<CancellationToken>()).Returns(_sala);
        _salas.EsMiembroAsync(_sala.Id, _autor.Id, Arg.Any<CancellationToken>()).Returns(true);
        _usuarios.ObtenerPorIdAsync(_autor.Id, Arg.Any<CancellationToken>()).Returns(_autor);
        _cifrador.Cifrar(Arg.Any<string>()).Returns(llamada => $"cifrado({llamada.Arg<string>()})");
    }

    private ManejadorEnviarMensaje Manejador(int longitudMaxima = 2000) => new(
        _mensajes,
        _salas,
        _usuarios,
        _adjuntos,
        _cifrador,
        _protector,
        _notificador,
        _unidadDeTrabajo,
        _reloj,
        Opciones.De(Opciones.Cifrado(longitudMaxima)),
        NullLogger<ManejadorEnviarMensaje>.Instance);

    private ComandoEnviarMensaje Comando(string texto = "hola", Guid? adjuntoId = null)
        => new(_autor.Id, Datos.SolicitudMensaje(_sala.Id, texto, adjuntoId));

    [Fact]
    public async Task UnMensajeDeTextoSeCifraSePersisteYSeDifunde()
    {
        var dto = await Manejador().ManejarAsync(Comando());

        await _mensajes.Received(1).AgregarAsync(
            Arg.Is<Mensaje>(m =>
                m.SalaId == _sala.Id
                && m.UsuarioId == _autor.Id
                && m.TextoCifrado == "cifrado(hola)"
                && m.FechaEnvio == _reloj.Ahora),
            Arg.Any<CancellationToken>());

        await _unidadDeTrabajo.Received(1).GuardarCambiosAsync(Arg.Any<CancellationToken>());
        await _notificador.Received(1).NotificarMensajeAsync(dto, Arg.Any<CancellationToken>());

        // Al cliente se le devuelve el texto en claro; el criptograma no sale de aquí.
        Assert.Equal("hola", dto.Texto);
        Assert.Equal("General", dto.SalaNombre);
        Assert.Equal("ana", dto.NombreUsuario);
        Assert.Null(dto.Adjunto);
    }

    [Fact]
    public async Task PublicarActualizaLaMarcaDeActividadDeLaSala()
    {
        // Es lo que ordena la bandeja del cliente por conversación más reciente.
        Assert.Null(_sala.FechaUltimaActividad);

        await Manejador().ManejarAsync(Comando());

        Assert.Equal(_reloj.Ahora, _sala.FechaUltimaActividad);
    }

    [Fact]
    public async Task LosAtajosDeEmojiSeExpandenAntesDeCifrar()
    {
        var dto = await Manejador().ManejarAsync(Comando(":fuego:"));

        Assert.Equal("\U0001F525", dto.Texto);
        _cifrador.Received(1).Cifrar("\U0001F525");
    }

    [Fact]
    public async Task ElLimiteDeLongitudSeMideSobreElEmojiYaExpandido()
    {
        // «:fuego:» son siete caracteres tecleados pero un emoji de dos: lo que cuenta
        // es el resultado, no lo que el usuario escribió.
        var dto = await Manejador(longitudMaxima: 2).ManejarAsync(Comando(":fuego:"));

        Assert.Equal("\U0001F525", dto.Texto);
    }

    [Fact]
    public async Task UnMensajeQueSupereElLimiteSeRechaza()
        => await Assert.ThrowsAsync<ExcepcionValidacion>(
            () => Manejador(longitudMaxima: 5).ManejarAsync(Comando("demasiado largo")));

    [Fact]
    public async Task UnMensajeVacioSinImagenSeRechaza()
        => await Assert.ThrowsAsync<ExcepcionValidacion>(() => Manejador().ManejarAsync(Comando("   ")));

    [Fact]
    public async Task UnIdentificadorDeEnvioRepetidoSeRechazaComoConflicto()
    {
        _protector.RegistrarSiEsNuevoAsync("mensaje", Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);

        await Assert.ThrowsAsync<ExcepcionConflicto>(() => Manejador().ManejarAsync(Comando()));
        await _mensajes.DidNotReceiveWithAnyArgs().AgregarAsync(default!);
    }

    [Fact]
    public async Task NoSePuedeEscribirEnUnaSalaQueNoExiste()
    {
        _salas.ObtenerPorIdAsync(_sala.Id, Arg.Any<CancellationToken>()).Returns((Sala?)null);

        await Assert.ThrowsAsync<ExcepcionNoEncontrado>(() => Manejador().ManejarAsync(Comando()));
    }

    [Fact]
    public async Task NoSePuedeEscribirEnUnaSalaDeLaQueNoSeEsMiembro()
    {
        _salas.EsMiembroAsync(_sala.Id, _autor.Id, Arg.Any<CancellationToken>()).Returns(false);

        await Assert.ThrowsAsync<ExcepcionAutorizacion>(() => Manejador().ManejarAsync(Comando()));
        await _mensajes.DidNotReceiveWithAnyArgs().AgregarAsync(default!);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task LosIdentificadoresVaciosSeRechazan(bool usuarioVacio, bool salaVacia)
    {
        var comando = new ComandoEnviarMensaje(
            usuarioVacio ? Guid.Empty : _autor.Id,
            Datos.SolicitudMensaje(salaVacia ? Guid.Empty : _sala.Id));

        await Assert.ThrowsAsync<ExcepcionValidacion>(() => Manejador().ManejarAsync(comando));
    }

    [Fact]
    public async Task UnIdentificadorDeEnvioVacioSeRechaza()
    {
        var comando = new ComandoEnviarMensaje(
            _autor.Id,
            new SolicitudEnviarMensajeDto(_sala.Id, "hola", Guid.Empty));

        await Assert.ThrowsAsync<ExcepcionValidacion>(() => Manejador().ManejarAsync(comando));
    }

    [Fact]
    public async Task UnaImagenSinPieDeFotoSePublicaSinTexto()
    {
        var adjunto = PrepararAdjunto();

        var dto = await Manejador().ManejarAsync(Comando(texto: "   ", adjuntoId: adjunto.Id));

        Assert.Equal(string.Empty, dto.Texto);
        Assert.NotNull(dto.Adjunto);
        Assert.Equal(adjunto.Id, dto.Adjunto.Id);

        // Sin pie de foto no hay nada que cifrar ni que guardar como texto.
        _cifrador.DidNotReceiveWithAnyArgs().Cifrar(default!);
        await _mensajes.Received(1).AgregarAsync(
            Arg.Is<Mensaje>(m => m.TextoCifrado == null && m.AdjuntoId == adjunto.Id),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UnaImagenConPieDeFotoLoCifraIgualQueUnMensaje()
    {
        var adjunto = PrepararAdjunto();

        var dto = await Manejador().ManejarAsync(Comando(texto: "mira esto", adjuntoId: adjunto.Id));

        Assert.Equal("mira esto", dto.Texto);
        _cifrador.Received(1).Cifrar("mira esto");
    }

    [Fact]
    public async Task NoSePuedePublicarUnAdjuntoQueNoExiste()
    {
        var adjuntoId = Guid.CreateVersion7();
        _adjuntos.ObtenerPorIdAsync(adjuntoId, Arg.Any<CancellationToken>()).Returns((Adjunto?)null);

        await Assert.ThrowsAsync<ExcepcionNoEncontrado>(
            () => Manejador().ManejarAsync(Comando(adjuntoId: adjuntoId)));
    }

    [Fact]
    public async Task NoSePuedePublicarLaImagenQueSubioOtraPersona()
    {
        // Conocer el identificador no basta: el adjunto es de quien lo subió.
        var ajeno = Datos.Adjunto(_sala.Id, Guid.CreateVersion7());
        _adjuntos.ObtenerPorIdAsync(ajeno.Id, Arg.Any<CancellationToken>()).Returns(ajeno);

        await Assert.ThrowsAsync<ExcepcionAutorizacion>(
            () => Manejador().ManejarAsync(Comando(adjuntoId: ajeno.Id)));
    }

    [Fact]
    public async Task NoSePuedeColarUnaImagenDeOtraConversacion()
    {
        var deOtraSala = Datos.Adjunto(Guid.CreateVersion7(), _autor.Id);
        _adjuntos.ObtenerPorIdAsync(deOtraSala.Id, Arg.Any<CancellationToken>()).Returns(deOtraSala);

        await Assert.ThrowsAsync<ExcepcionAutorizacion>(
            () => Manejador().ManejarAsync(Comando(adjuntoId: deOtraSala.Id)));
    }

    [Fact]
    public async Task UnaImagenNoSePuedePublicarDosVeces()
    {
        var adjunto = PrepararAdjunto();
        _adjuntos.EstaPublicadoAsync(adjunto.Id, Arg.Any<CancellationToken>()).Returns(true);

        await Assert.ThrowsAsync<ExcepcionConflicto>(
            () => Manejador().ManejarAsync(Comando(adjuntoId: adjunto.Id)));
    }

    [Fact]
    public async Task UnAdjuntoVacioSeTrataComoMensajeDeSoloTexto()
    {
        // Un identificador vacío es «sin adjunto», no un adjunto que haya que buscar.
        var comando = new ComandoEnviarMensaje(
            _autor.Id,
            new SolicitudEnviarMensajeDto(_sala.Id, "hola", Guid.CreateVersion7(), Guid.Empty));

        var dto = await Manejador().ManejarAsync(comando);

        Assert.Null(dto.Adjunto);
        await _adjuntos.DidNotReceiveWithAnyArgs().ObtenerPorIdAsync(default);
    }

    [Fact]
    public async Task EnUnaConversacionDirectaElMensajeViajaConElNombreDelAutor()
    {
        // El nombre almacenado de una directa es un identificador interno que no debe
        // salir del servidor: el destinatario reconoce la conversación por quien escribe.
        var directa = Datos.Sala(nombre: "directa:abc:def", tipo: TipoSala.Directa, claveDirecta: "abc:def");

        _salas.ObtenerPorIdAsync(directa.Id, Arg.Any<CancellationToken>()).Returns(directa);
        _salas.EsMiembroAsync(directa.Id, _autor.Id, Arg.Any<CancellationToken>()).Returns(true);

        var dto = await Manejador().ManejarAsync(
            new ComandoEnviarMensaje(_autor.Id, Datos.SolicitudMensaje(directa.Id)));

        Assert.Equal("ana", dto.SalaNombre);
    }

    [Fact]
    public async Task SiElAutorNoExisteElEnvioFalla()
    {
        _usuarios.ObtenerPorIdAsync(_autor.Id, Arg.Any<CancellationToken>()).Returns((Usuario?)null);

        await Assert.ThrowsAsync<ExcepcionNoEncontrado>(() => Manejador().ManejarAsync(Comando()));
    }

    [Fact]
    public async Task UnComandoNuloSeRechaza()
        => await Assert.ThrowsAsync<ArgumentNullException>(() => Manejador().ManejarAsync(null!));

    /// <summary>Registra un adjunto válido del autor y para la sala de la prueba.</summary>
    private Adjunto PrepararAdjunto()
    {
        var adjunto = Datos.Adjunto(_sala.Id, _autor.Id);
        _adjuntos.ObtenerPorIdAsync(adjunto.Id, Arg.Any<CancellationToken>()).Returns(adjunto);
        _adjuntos.EstaPublicadoAsync(adjunto.Id, Arg.Any<CancellationToken>()).Returns(false);

        return adjunto;
    }
}
