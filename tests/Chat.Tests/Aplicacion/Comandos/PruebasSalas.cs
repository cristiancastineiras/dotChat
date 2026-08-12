using Chat.Aplicacion.Abstracciones;
using Chat.Aplicacion.Comandos.Salas;
using Chat.Aplicacion.Dtos;
using Chat.Dominio.Abstracciones;
using Chat.Dominio.Entidades;
using Chat.Dominio.Excepciones;
using Chat.Tests.Comun;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Chat.Tests.Aplicacion.Comandos;

/// <summary>Pruebas de la creación de salas.</summary>
public sealed class PruebasCrearSala
{
    private readonly IRepositorioSalas _salas = Substitute.For<IRepositorioSalas>();
    private readonly IUnidadDeTrabajo _unidadDeTrabajo = Substitute.For<IUnidadDeTrabajo>();
    private readonly INotificadorTiempoReal _notificador = Substitute.For<INotificadorTiempoReal>();
    private readonly CacheDePrueba _cache = new();
    private readonly RelojFijo _reloj = new();
    private readonly Guid _creadorId = Guid.CreateVersion7();

    private ManejadorCrearSala Manejador => new(
        _salas,
        _unidadDeTrabajo,
        _cache,
        _notificador,
        _reloj,
        NullLogger<ManejadorCrearSala>.Instance);

    private ComandoCrearSala Comando(string nombre = "Equipo", string? descripcion = null, bool privada = false)
        => new(new SolicitudCrearSalaDto(nombre, descripcion, privada), _creadorId);

    [Fact]
    public async Task UnaSalaPublicaSeCreaConSuCreadorDentroYSeAnuncia()
    {
        var dto = await Manejador.ManejarAsync(Comando(descripcion: "  Sala del  equipo "));

        Assert.Equal("Equipo", dto.Nombre);
        Assert.Equal("Sala del equipo", dto.Descripcion);
        Assert.Equal(TipoSala.Publica, dto.Tipo);
        Assert.Equal(1, dto.TotalMiembros);
        Assert.True(dto.EsMiembro);

        await _salas.Received(1).AgregarMembresiaAsync(
            Arg.Is<MiembroSala>(m => m.UsuarioId == _creadorId && m.FechaUltimaLectura == _reloj.Ahora),
            Arg.Any<CancellationToken>());

        await _unidadDeTrabajo.Received(1).GuardarCambiosAsync(Arg.Any<CancellationToken>());
        await _notificador.Received(1).NotificarSalaCreadaAsync(dto, Arg.Any<CancellationToken>());
        Assert.Contains(ClavesCache.EtiquetaSalas, _cache.EtiquetasInvalidadas);
    }

    [Fact]
    public async Task UnaSalaPrivadaNoSeAnunciaANadie()
    {
        // Anunciarla delataría su existencia a quien no ha sido invitado.
        var dto = await Manejador.ManejarAsync(Comando(privada: true));

        Assert.Equal(TipoSala.Privada, dto.Tipo);
        await _notificador.DidNotReceiveWithAnyArgs().NotificarSalaCreadaAsync(default!);
    }

    [Fact]
    public async Task UnNombreYaExistenteSeRechaza()
    {
        _salas.ObtenerPorNombreAsync("Equipo", Arg.Any<CancellationToken>()).Returns(Datos.Sala(nombre: "Equipo"));

        var excepcion = await Assert.ThrowsAsync<ExcepcionConflicto>(() => Manejador.ManejarAsync(Comando()));

        Assert.Contains("Equipo", excepcion.Message, StringComparison.Ordinal);
        await _salas.DidNotReceiveWithAnyArgs().AgregarAsync(default!);
    }

    [Theory]
    [InlineData("ab")]
    [InlineData("-mala-")]
    public async Task UnNombreMalFormadoSeRechaza(string nombre)
        => await Assert.ThrowsAsync<ExcepcionValidacion>(() => Manejador.ManejarAsync(Comando(nombre)));

    [Fact]
    public async Task UnCreadorSinIdentificarSeRechaza()
    {
        var comando = new ComandoCrearSala(new SolicitudCrearSalaDto("Equipo", null), Guid.Empty);

        await Assert.ThrowsAsync<ExcepcionValidacion>(() => Manejador.ManejarAsync(comando));
    }
}

/// <summary>Pruebas de la incorporación a una sala.</summary>
public sealed class PruebasUnirseSala
{
    private readonly IRepositorioSalas _salas = Substitute.For<IRepositorioSalas>();
    private readonly IUnidadDeTrabajo _unidadDeTrabajo = Substitute.For<IUnidadDeTrabajo>();
    private readonly CacheDePrueba _cache = new();
    private readonly RelojFijo _reloj = new();
    private readonly Guid _usuarioId = Guid.CreateVersion7();

    private ManejadorUnirseSala Manejador => new(
        _salas,
        _unidadDeTrabajo,
        _cache,
        _reloj,
        NullLogger<ManejadorUnirseSala>.Instance);

    [Fact]
    public async Task CualquieraPuedeEntrarEnUnaSalaPublica()
    {
        var sala = Preparar(TipoSala.Publica);

        var dto = await Manejador.ManejarAsync(new ComandoUnirseSala(sala.Id, _usuarioId));

        await _salas.Received(1).AgregarMembresiaAsync(
            Arg.Is<MiembroSala>(m => m.SalaId == sala.Id && m.UsuarioId == _usuarioId && m.FechaUnion == _reloj.Ahora),
            Arg.Any<CancellationToken>());

        await _unidadDeTrabajo.Received(1).GuardarCambiosAsync(Arg.Any<CancellationToken>());
        Assert.Contains(ClavesCache.EtiquetaSalas, _cache.EtiquetasInvalidadas);
        Assert.True(dto.EsMiembro);
    }

    [Fact]
    public async Task VolverAUnirseNoDuplicaLaMembresia()
    {
        // La operación es idempotente: el cliente puede reintentar sin miedo.
        var sala = Preparar(TipoSala.Publica);
        _salas
            .ObtenerMembresiaAsync(sala.Id, _usuarioId, Arg.Any<CancellationToken>())
            .Returns(Datos.Membresia(sala.Id, _usuarioId));

        var dto = await Manejador.ManejarAsync(new ComandoUnirseSala(sala.Id, _usuarioId));

        await _salas.DidNotReceiveWithAnyArgs().AgregarMembresiaAsync(default!);
        await _unidadDeTrabajo.DidNotReceiveWithAnyArgs().GuardarCambiosAsync();
        Assert.True(dto.EsMiembro);
    }

    [Fact]
    public async Task AUnaSalaPrivadaHayQueSerInvitado()
    {
        var sala = Preparar(TipoSala.Privada);

        var excepcion = await Assert.ThrowsAsync<ExcepcionAutorizacion>(
            () => Manejador.ManejarAsync(new ComandoUnirseSala(sala.Id, _usuarioId)));

        Assert.Contains("invitarte", excepcion.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NoSePuedeEntrarEnUnaConversacionDirectaAjena()
    {
        var sala = Preparar(TipoSala.Directa);

        var excepcion = await Assert.ThrowsAsync<ExcepcionAutorizacion>(
            () => Manejador.ManejarAsync(new ComandoUnirseSala(sala.Id, _usuarioId)));

        Assert.Contains("conversación directa", excepcion.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnMiembroDeUnaDirectaLaVeConElNombreDelInterlocutor()
    {
        var yo = Datos.Usuario(id: _usuarioId, nombre: "ana");
        var otra = Datos.Usuario(nombre: "eva");
        var sala = Datos.Sala(tipo: TipoSala.Directa, nombre: "directa:x:y", claveDirecta: "x:y");

        _salas.ObtenerPorIdAsync(sala.Id, Arg.Any<CancellationToken>()).Returns(sala);
        _salas
            .ObtenerMembresiaAsync(sala.Id, _usuarioId, Arg.Any<CancellationToken>())
            .Returns(Datos.Membresia(sala.Id, _usuarioId));
        _salas.ListarMiembrosAsync(sala.Id, Arg.Any<CancellationToken>()).Returns(
        [
            Datos.Membresia(sala.Id, yo.Id, yo),
            Datos.Membresia(sala.Id, otra.Id, otra)
        ]);

        var dto = await Manejador.ManejarAsync(new ComandoUnirseSala(sala.Id, _usuarioId));

        Assert.Equal("eva", dto.Nombre);
        Assert.Equal(2, dto.TotalMiembros);
    }

    [Fact]
    public async Task UnaSalaInexistenteSeRechaza()
    {
        var salaId = Guid.CreateVersion7();
        _salas.ObtenerPorIdAsync(salaId, Arg.Any<CancellationToken>()).Returns((Sala?)null);

        await Assert.ThrowsAsync<ExcepcionNoEncontrado>(
            () => Manejador.ManejarAsync(new ComandoUnirseSala(salaId, _usuarioId)));
    }

    [Fact]
    public async Task LosIdentificadoresVaciosSeRechazan()
    {
        await Assert.ThrowsAsync<ExcepcionValidacion>(
            () => Manejador.ManejarAsync(new ComandoUnirseSala(Guid.Empty, _usuarioId)));

        await Assert.ThrowsAsync<ExcepcionValidacion>(
            () => Manejador.ManejarAsync(new ComandoUnirseSala(Guid.CreateVersion7(), Guid.Empty)));
    }

    /// <summary>Deja una sala del tipo indicado disponible y sin el usuario dentro.</summary>
    /// <param name="tipo">Naturaleza de la sala.</param>
    private Sala Preparar(TipoSala tipo)
    {
        var sala = Datos.Sala(tipo: tipo);

        _salas.ObtenerPorIdAsync(sala.Id, Arg.Any<CancellationToken>()).Returns(sala);
        _salas.ObtenerMembresiaAsync(sala.Id, _usuarioId, Arg.Any<CancellationToken>()).Returns((MiembroSala?)null);
        _salas.ListarMiembrosAsync(sala.Id, Arg.Any<CancellationToken>()).Returns([Datos.Membresia(sala.Id, _usuarioId)]);

        return sala;
    }
}

/// <summary>Pruebas de la salida de una sala.</summary>
public sealed class PruebasSalirSala
{
    private readonly IRepositorioSalas _salas = Substitute.For<IRepositorioSalas>();
    private readonly IUnidadDeTrabajo _unidadDeTrabajo = Substitute.For<IUnidadDeTrabajo>();
    private readonly CacheDePrueba _cache = new();
    private readonly Guid _usuarioId = Guid.CreateVersion7();
    private readonly Sala _sala = Datos.Sala();

    public PruebasSalirSala()
        => _salas.ObtenerPorIdAsync(_sala.Id, Arg.Any<CancellationToken>()).Returns(_sala);

    private ManejadorSalirSala Manejador => new(
        _salas,
        _unidadDeTrabajo,
        _cache,
        NullLogger<ManejadorSalirSala>.Instance);

    [Fact]
    public async Task SalirEliminaLaMembresiaEInvalidaElCatalogo()
    {
        var membresia = Datos.Membresia(_sala.Id, _usuarioId);
        _salas.ObtenerMembresiaAsync(_sala.Id, _usuarioId, Arg.Any<CancellationToken>()).Returns(membresia);

        var resultado = await Manejador.ManejarAsync(new ComandoSalirSala(_sala.Id, _usuarioId));

        Assert.True(resultado.Exito);
        _salas.Received(1).EliminarMembresia(membresia);
        await _unidadDeTrabajo.Received(1).GuardarCambiosAsync(Arg.Any<CancellationToken>());
        Assert.Contains(ClavesCache.EtiquetaSalas, _cache.EtiquetasInvalidadas);
    }

    [Fact]
    public async Task SalirDeUnaSalaALaQueNoSePerteneceNoEsUnError()
    {
        _salas.ObtenerMembresiaAsync(_sala.Id, _usuarioId, Arg.Any<CancellationToken>()).Returns((MiembroSala?)null);

        var resultado = await Manejador.ManejarAsync(new ComandoSalirSala(_sala.Id, _usuarioId));

        Assert.True(resultado.Exito);
        Assert.Contains("No pertenecías", resultado.Mensaje, StringComparison.Ordinal);
        await _unidadDeTrabajo.DidNotReceiveWithAnyArgs().GuardarCambiosAsync();
    }

    [Fact]
    public async Task NoSePuedeSalirDeUnaSalaQueNoExiste()
    {
        var salaId = Guid.CreateVersion7();
        _salas.ObtenerPorIdAsync(salaId, Arg.Any<CancellationToken>()).Returns((Sala?)null);

        await Assert.ThrowsAsync<ExcepcionNoEncontrado>(
            () => Manejador.ManejarAsync(new ComandoSalirSala(salaId, _usuarioId)));
    }
}

/// <summary>Pruebas de las invitaciones.</summary>
public sealed class PruebasInvitarASala
{
    private readonly IRepositorioSalas _salas = Substitute.For<IRepositorioSalas>();
    private readonly IRepositorioUsuarios _usuarios = Substitute.For<IRepositorioUsuarios>();
    private readonly IUnidadDeTrabajo _unidadDeTrabajo = Substitute.For<IUnidadDeTrabajo>();
    private readonly INotificadorTiempoReal _notificador = Substitute.For<INotificadorTiempoReal>();
    private readonly CacheDePrueba _cache = new();
    private readonly RelojFijo _reloj = new();

    private readonly Sala _sala = Datos.Sala(tipo: TipoSala.Privada, nombre: "Equipo");
    private readonly Guid _anfitrionId = Guid.CreateVersion7();
    private readonly Usuario _invitado = Datos.Usuario(nombre: "eva");

    public PruebasInvitarASala()
    {
        _salas.ObtenerPorIdAsync(_sala.Id, Arg.Any<CancellationToken>()).Returns(_sala);
        _salas.EsMiembroAsync(_sala.Id, _anfitrionId, Arg.Any<CancellationToken>()).Returns(true);
        _salas.ContarMiembrosAsync(_sala.Id, Arg.Any<CancellationToken>()).Returns(2);
        _usuarios.ObtenerPorNombreAsync("eva", Arg.Any<CancellationToken>()).Returns(_invitado);
    }

    private ManejadorInvitarASala Manejador => new(
        _salas,
        _usuarios,
        _unidadDeTrabajo,
        _cache,
        _notificador,
        _reloj,
        NullLogger<ManejadorInvitarASala>.Instance);

    private ComandoInvitarASala Comando(string nombre = "eva")
        => new(_sala.Id, _anfitrionId, new SolicitudInvitarDto(nombre));

    [Fact]
    public async Task ElInvitadoEntraYRecibeLaSalaAlInstante()
    {
        var resultado = await Manejador.ManejarAsync(Comando());

        Assert.True(resultado.Exito);

        await _salas.Received(1).AgregarMembresiaAsync(
            Arg.Is<MiembroSala>(m => m.UsuarioId == _invitado.Id && m.FechaUnion == _reloj.Ahora),
            Arg.Any<CancellationToken>());

        // Si está conectado, sus clientes se suscriben sin reiniciar la sesión.
        await _notificador.Received(1).NotificarSalaDisponibleAsync(
            _invitado.Id,
            Arg.Is<SalaDto>(s => s.Id == _sala.Id && s.TotalMiembros == 2 && s.EsMiembro),
            Arg.Any<CancellationToken>());

        await _notificador.Received(1).NotificarUsuarioUnidoAsync("Equipo", "eva", Arg.Any<CancellationToken>());
        Assert.Contains(ClavesCache.EtiquetaSalas, _cache.EtiquetasInvalidadas);
    }

    [Fact]
    public async Task InvitarAQuienYaEstabaDentroNoCambiaNada()
    {
        _salas
            .ObtenerMembresiaAsync(_sala.Id, _invitado.Id, Arg.Any<CancellationToken>())
            .Returns(Datos.Membresia(_sala.Id, _invitado.Id));

        var resultado = await Manejador.ManejarAsync(Comando());

        Assert.True(resultado.Exito);
        Assert.Contains("ya pertenecía", resultado.Mensaje, StringComparison.Ordinal);
        await _salas.DidNotReceiveWithAnyArgs().AgregarMembresiaAsync(default!);
        await _notificador.DidNotReceiveWithAnyArgs().NotificarSalaDisponibleAsync(default, default!);
    }

    [Fact]
    public async Task SoloUnMiembroPuedeInvitar()
    {
        _salas.EsMiembroAsync(_sala.Id, _anfitrionId, Arg.Any<CancellationToken>()).Returns(false);

        await Assert.ThrowsAsync<ExcepcionAutorizacion>(() => Manejador.ManejarAsync(Comando()));
    }

    [Fact]
    public async Task UnaConversacionDirectaNoAdmiteInvitados()
    {
        // Una directa es cosa de dos por definición: meter a un tercero rompería la
        // correspondencia entre su clave canónica y sus participantes.
        var directa = Datos.Sala(tipo: TipoSala.Directa, claveDirecta: "x:y");
        _salas.ObtenerPorIdAsync(directa.Id, Arg.Any<CancellationToken>()).Returns(directa);

        var comando = new ComandoInvitarASala(directa.Id, _anfitrionId, new SolicitudInvitarDto("eva"));

        await Assert.ThrowsAsync<ExcepcionAutorizacion>(() => Manejador.ManejarAsync(comando));
    }

    [Fact]
    public async Task NoSePuedeInvitarAQuienNoExiste()
    {
        _usuarios.ObtenerPorNombreAsync("nadie", Arg.Any<CancellationToken>()).Returns((Usuario?)null);

        await Assert.ThrowsAsync<ExcepcionNoEncontrado>(() => Manejador.ManejarAsync(Comando("nadie")));
    }

    [Fact]
    public async Task NoSePuedeInvitarAUnaCuentaDesactivada()
    {
        _usuarios
            .ObtenerPorNombreAsync("eva", Arg.Any<CancellationToken>())
            .Returns(Datos.Usuario(nombre: "eva", activo: false));

        await Assert.ThrowsAsync<ExcepcionConflicto>(() => Manejador.ManejarAsync(Comando()));
    }

    [Fact]
    public async Task NoSePuedeInvitarAUnaSalaQueNoExiste()
    {
        var salaId = Guid.CreateVersion7();
        _salas.ObtenerPorIdAsync(salaId, Arg.Any<CancellationToken>()).Returns((Sala?)null);

        var comando = new ComandoInvitarASala(salaId, _anfitrionId, new SolicitudInvitarDto("eva"));

        await Assert.ThrowsAsync<ExcepcionNoEncontrado>(() => Manejador.ManejarAsync(comando));
    }
}

/// <summary>Pruebas de la apertura de conversaciones directas.</summary>
public sealed class PruebasAbrirConversacionDirecta
{
    private readonly IRepositorioSalas _salas = Substitute.For<IRepositorioSalas>();
    private readonly IRepositorioUsuarios _usuarios = Substitute.For<IRepositorioUsuarios>();
    private readonly IUnidadDeTrabajo _unidadDeTrabajo = Substitute.For<IUnidadDeTrabajo>();
    private readonly INotificadorTiempoReal _notificador = Substitute.For<INotificadorTiempoReal>();
    private readonly CacheDePrueba _cache = new();
    private readonly RelojFijo _reloj = new();

    private readonly Usuario _ana = Datos.Usuario(nombre: "ana");
    private readonly Usuario _eva = Datos.Usuario(nombre: "eva");

    public PruebasAbrirConversacionDirecta()
    {
        _usuarios.ObtenerPorIdAsync(_ana.Id, Arg.Any<CancellationToken>()).Returns(_ana);
        _usuarios.ObtenerPorIdAsync(_eva.Id, Arg.Any<CancellationToken>()).Returns(_eva);
        _usuarios.ObtenerPorNombreAsync("eva", Arg.Any<CancellationToken>()).Returns(_eva);
    }

    private ManejadorAbrirConversacionDirecta Manejador => new(
        _salas,
        _usuarios,
        _unidadDeTrabajo,
        _cache,
        _notificador,
        _reloj,
        NullLogger<ManejadorAbrirConversacionDirecta>.Instance);

    private ComandoAbrirConversacionDirecta Comando(string? nombre = "eva", Guid? id = null)
        => new(_ana.Id, new SolicitudConversacionDirectaDto(nombre, id));

    [Fact]
    public async Task AbrirUnaConversacionNuevaLaCreaConSusDosMiembros()
    {
        var dto = await Manejador.ManejarAsync(Comando());

        // Quien la abre la ve como «eva»; el nombre almacenado es interno.
        Assert.Equal("eva", dto.Nombre);
        Assert.Equal(TipoSala.Directa, dto.Tipo);
        Assert.Equal(2, dto.TotalMiembros);
        Assert.True(dto.EsMiembro);
        Assert.True(dto.EsDirecta);

        await _salas.Received(1).AgregarAsync(
            Arg.Is<Sala>(s =>
                s.Tipo == TipoSala.Directa
                && s.ClaveDirecta == Sala.ConstruirClaveDirecta(_ana.Id, _eva.Id)
                && s.CreadorId == _ana.Id),
            Arg.Any<CancellationToken>());

        await _salas.Received(2).AgregarMembresiaAsync(Arg.Any<MiembroSala>(), Arg.Any<CancellationToken>());
        await _unidadDeTrabajo.Received(1).GuardarCambiosAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task QuienAbreLaConversacionLaEmpiezaLeida()
    {
        // Su propia conversación recién abierta no debería contarle como pendiente.
        await Manejador.ManejarAsync(Comando());

        await _salas.Received(1).AgregarMembresiaAsync(
            Arg.Is<MiembroSala>(m => m.UsuarioId == _ana.Id && m.FechaUltimaLectura == _reloj.Ahora),
            Arg.Any<CancellationToken>());

        await _salas.Received(1).AgregarMembresiaAsync(
            Arg.Is<MiembroSala>(m => m.UsuarioId == _eva.Id && m.FechaUltimaLectura == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AlInterlocutorSeLeAvisaConElNombreDeQuienLeEscribe()
    {
        await Manejador.ManejarAsync(Comando());

        await _notificador.Received(1).NotificarSalaDisponibleAsync(
            _eva.Id,
            Arg.Is<SalaDto>(s => s.Nombre == "ana" && s.EsMiembro),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AbrirDosVecesDevuelveLaMismaConversacion()
    {
        // Es idempotente: si no lo fuera, cada intento crearía un hilo nuevo y el
        // historial quedaría repartido entre varios.
        var existente = Datos.Sala(
            tipo: TipoSala.Directa,
            claveDirecta: Sala.ConstruirClaveDirecta(_ana.Id, _eva.Id));

        _salas
            .ObtenerPorClaveDirectaAsync(existente.ClaveDirecta!, Arg.Any<CancellationToken>())
            .Returns(existente);

        var dto = await Manejador.ManejarAsync(Comando());

        Assert.Equal(existente.Id, dto.Id);
        Assert.Equal("eva", dto.Nombre);
        await _salas.DidNotReceiveWithAnyArgs().AgregarAsync(default!);
        await _notificador.DidNotReceiveWithAnyArgs().NotificarSalaDisponibleAsync(default, default!);
    }

    [Fact]
    public async Task ElInterlocutorSePuedeIndicarPorIdentificador()
    {
        var dto = await Manejador.ManejarAsync(Comando(nombre: null, id: _eva.Id));

        Assert.Equal("eva", dto.Nombre);
    }

    [Fact]
    public async Task ElIdentificadorTienePrioridadSobreElNombre()
    {
        var leo = Datos.Usuario(nombre: "leo");
        _usuarios.ObtenerPorIdAsync(leo.Id, Arg.Any<CancellationToken>()).Returns(leo);

        var dto = await Manejador.ManejarAsync(Comando(nombre: "eva", id: leo.Id));

        Assert.Equal("leo", dto.Nombre);
    }

    [Fact]
    public async Task NoSePuedeHablarConsigoMismo()
        => await Assert.ThrowsAsync<ExcepcionValidacion>(
            () => Manejador.ManejarAsync(Comando(nombre: null, id: _ana.Id)));

    [Fact]
    public async Task NoSePuedeHablarConUnaCuentaDesactivada()
    {
        _usuarios
            .ObtenerPorNombreAsync("eva", Arg.Any<CancellationToken>())
            .Returns(Datos.Usuario(nombre: "eva", activo: false));

        await Assert.ThrowsAsync<ExcepcionConflicto>(() => Manejador.ManejarAsync(Comando()));
    }

    [Fact]
    public async Task HayQueIndicarConQuienSeQuiereHablar()
        => await Assert.ThrowsAsync<ExcepcionValidacion>(() => Manejador.ManejarAsync(Comando(nombre: null)));

    [Fact]
    public async Task UnInterlocutorDesconocidoSeRechaza()
    {
        _usuarios.ObtenerPorNombreAsync("nadie", Arg.Any<CancellationToken>()).Returns((Usuario?)null);

        await Assert.ThrowsAsync<ExcepcionNoEncontrado>(() => Manejador.ManejarAsync(Comando("nadie")));
    }

    [Fact]
    public async Task UnSolicitanteDesconocidoSeRechaza()
    {
        _usuarios.ObtenerPorIdAsync(_ana.Id, Arg.Any<CancellationToken>()).Returns((Usuario?)null);

        await Assert.ThrowsAsync<ExcepcionNoEncontrado>(() => Manejador.ManejarAsync(Comando()));
    }
}

/// <summary>Pruebas de la marca de lectura y del borrado de salas.</summary>
public sealed class PruebasMarcarSalaLeidaYEliminar
{
    private readonly IRepositorioSalas _salas = Substitute.For<IRepositorioSalas>();
    private readonly IUnidadDeTrabajo _unidadDeTrabajo = Substitute.For<IUnidadDeTrabajo>();
    private readonly CacheDePrueba _cache = new();
    private readonly RelojFijo _reloj = new();

    [Fact]
    public async Task MarcarLeidaAdelantaLaMarcaDelUsuario()
    {
        var salaId = Guid.CreateVersion7();
        var usuarioId = Guid.CreateVersion7();
        var membresia = Datos.Membresia(salaId, usuarioId);

        _salas.ObtenerMembresiaAsync(salaId, usuarioId, Arg.Any<CancellationToken>()).Returns(membresia);

        var resultado = await ManejadorLectura.ManejarAsync(new ComandoMarcarSalaLeida(salaId, usuarioId));

        Assert.True(resultado.Exito);
        Assert.Equal(_reloj.Ahora, membresia.FechaUltimaLectura);
        await _unidadDeTrabajo.Received(1).GuardarCambiosAsync(Arg.Any<CancellationToken>());

        // La marca es un dato por usuario: no tiene por qué tirar el catálogo cacheado.
        Assert.Empty(_cache.EtiquetasInvalidadas);
    }

    [Fact]
    public async Task SoloUnMiembroPuedeMarcarLaSalaComoLeida()
    {
        _salas
            .ObtenerMembresiaAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((MiembroSala?)null);

        await Assert.ThrowsAsync<ExcepcionAutorizacion>(() => ManejadorLectura.ManejarAsync(
            new ComandoMarcarSalaLeida(Guid.CreateVersion7(), Guid.CreateVersion7())));
    }

    [Fact]
    public async Task LosIdentificadoresVaciosSeRechazanAlMarcarLeida()
        => await Assert.ThrowsAsync<ExcepcionValidacion>(() => ManejadorLectura.ManejarAsync(
            new ComandoMarcarSalaLeida(Guid.Empty, Guid.CreateVersion7())));

    [Fact]
    public async Task EliminarUnaSalaSeLlevaSuHistorialEInvalidaElCatalogo()
    {
        var sala = Datos.Sala(nombre: "Equipo");
        _salas.ObtenerPorIdAsync(sala.Id, Arg.Any<CancellationToken>()).Returns(sala);

        var resultado = await ManejadorBorrado.ManejarAsync(new ComandoEliminarSala(sala.Id));

        Assert.True(resultado.Exito);
        Assert.Contains("Equipo", resultado.Mensaje, StringComparison.Ordinal);
        _salas.Received(1).Eliminar(sala);
        await _unidadDeTrabajo.Received(1).GuardarCambiosAsync(Arg.Any<CancellationToken>());
        Assert.Contains(ClavesCache.EtiquetaSalas, _cache.EtiquetasInvalidadas);
    }

    [Fact]
    public async Task NoSePuedeEliminarUnaSalaQueNoExiste()
    {
        var salaId = Guid.CreateVersion7();
        _salas.ObtenerPorIdAsync(salaId, Arg.Any<CancellationToken>()).Returns((Sala?)null);

        await Assert.ThrowsAsync<ExcepcionNoEncontrado>(
            () => ManejadorBorrado.ManejarAsync(new ComandoEliminarSala(salaId)));
    }

    [Fact]
    public async Task UnIdentificadorVacioSeRechazaAlEliminar()
        => await Assert.ThrowsAsync<ExcepcionValidacion>(
            () => ManejadorBorrado.ManejarAsync(new ComandoEliminarSala(Guid.Empty)));

    private ManejadorMarcarSalaLeida ManejadorLectura => new(_salas, _unidadDeTrabajo, _reloj);

    private ManejadorEliminarSala ManejadorBorrado => new(
        _salas,
        _unidadDeTrabajo,
        _cache,
        NullLogger<ManejadorEliminarSala>.Instance);
}
