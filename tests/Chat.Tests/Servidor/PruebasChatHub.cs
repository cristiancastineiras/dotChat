using System.Security.Claims;
using Chat.Aplicacion.Abstracciones;
using Chat.Aplicacion.Comandos.Mensajes;
using Chat.Aplicacion.Comandos.Salas;
using Chat.Aplicacion.Consultas.Salas;
using Chat.Aplicacion.Cqrs;
using Chat.Aplicacion.Dtos;
using Chat.Dominio.Entidades;
using Chat.Dominio.Excepciones;
using Chat.Servidor.Hubs;
using Chat.Tests.Comun;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.JsonWebTokens;
using NSubstitute;

namespace Chat.Tests.Servidor;

/// <summary>
/// Pruebas del concentrador de tiempo real. La identidad se toma siempre de los
/// claims, nunca de los parámetros que manda el cliente, y los errores de dominio se
/// devuelven como aviso en lugar de tumbar la invocación.
/// </summary>
public sealed class PruebasChatHub
{
    private readonly IDespachador _despachador = Substitute.For<IDespachador>();
    private readonly IRegistroConexiones _conexiones = Substitute.For<IRegistroConexiones>();
    private readonly INotificadorTiempoReal _notificador = Substitute.For<INotificadorTiempoReal>();
    private readonly ILimitadorEnvios _limitador = Substitute.For<ILimitadorEnvios>();
    private readonly RelojFijo _reloj = new();

    private readonly IHubCallerClients<IClienteChat> _clientes = Substitute.For<IHubCallerClients<IClienteChat>>();
    private readonly IClienteChat _llamante = Substitute.For<IClienteChat>();
    private readonly IClienteChat _otrosDelGrupo = Substitute.For<IClienteChat>();
    private readonly IGroupManager _grupos = Substitute.For<IGroupManager>();
    private readonly ContextoDeConexion _contexto;

    private readonly Guid _usuarioId = Guid.CreateVersion7();

    public PruebasChatHub()
    {
        _contexto = new ContextoDeConexion("conexion-1", _usuarioId, "ana");

        _clientes.Caller.Returns(_llamante);
        _clientes.OthersInGroup(Arg.Any<string>()).Returns(_otrosDelGrupo);

        _limitador.IntentarConsumirAsync(_usuarioId, Arg.Any<CancellationToken>()).Returns(true);

        _despachador
            .ConsultarAsync(Arg.Any<IConsulta<IReadOnlyList<SalaDto>>>(), Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<SalaDto>>(_ => []);
    }

    private ChatHub Hub()
    {
        var hub = new ChatHub(
            _despachador,
            _conexiones,
            _notificador,
            _limitador,
            _reloj,
            NullLogger<ChatHub>.Instance)
        {
            Context = _contexto,
            Clients = _clientes,
            Groups = _grupos
        };

        return hub;
    }

    [Fact]
    public void ElNombreDeGrupoDependeSoloDeLaSala()
    {
        var salaId = Guid.CreateVersion7();

        Assert.Equal($"sala:{salaId}", ChatHub.NombreGrupo(salaId));
        Assert.Equal(ChatHub.NombreGrupo(salaId), ChatHub.NombreGrupo(salaId));
    }

    [Fact]
    public async Task AlConectarSeSuscribeATodasSusSalasYSeLeDevuelveElEstadoInicial()
    {
        var salas = new[] { Sala("General"), Sala("Equipo") };
        DevolverSalas(salas);

        await Hub().Conectar();

        await _conexiones.Received(1).RegistrarAsync(
            "conexion-1", _usuarioId, "ana", _reloj.Ahora, Arg.Any<CancellationToken>());

        foreach (var sala in salas)
        {
            await _grupos.Received(1).AddToGroupAsync(
                "conexion-1", ChatHub.NombreGrupo(sala.Id), Arg.Any<CancellationToken>());

            await _conexiones.Received(1).AgregarSalaAsync(
                "conexion-1", sala.Nombre, Arg.Any<CancellationToken>());
        }

        await _llamante.Received(1).Conectado("ana", Arg.Is<IReadOnlyList<SalaDto>>(s => s.Count == 2));
    }

    [Fact]
    public async Task LaPrimeraConexionDeUnUsuarioSeAnunciaALosDemas()
    {
        _conexiones
            .RegistrarAsync(Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(true);

        await Hub().Conectar();

        await _notificador.Received(1).NotificarPresenciaAsync(
            Arg.Is<PresenciaDto>(p => p.UsuarioId == _usuarioId && p.EnLinea && p.Conexiones == 1),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UnaSegundaConexionDelMismoUsuarioNoVuelveAAnunciarlo()
    {
        _conexiones
            .RegistrarAsync(Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(false);

        await Hub().Conectar();

        await _notificador.DidNotReceiveWithAnyArgs().NotificarPresenciaAsync(default!);
    }

    [Fact]
    public async Task AlCerrarseLaUltimaConexionSeAnunciaLaDesconexion()
    {
        _conexiones
            .EliminarAsync("conexion-1", Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(new ConexionCerrada(_usuarioId, "ana", FueLaUltima: true));

        await Hub().Desconectar();

        await _notificador.Received(1).NotificarPresenciaAsync(
            Arg.Is<PresenciaDto>(p => p.UsuarioId == _usuarioId && !p.EnLinea && p.Conexiones == 0),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CerrarUnaDeVariasConexionesNoAnunciaNada()
    {
        _conexiones
            .EliminarAsync("conexion-1", Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(new ConexionCerrada(_usuarioId, "ana", FueLaUltima: false));

        await Hub().Desconectar();

        await _notificador.DidNotReceiveWithAnyArgs().NotificarPresenciaAsync(default!);
    }

    [Fact]
    public async Task LaLimpiezaAlDesconectarNoUsaElTokenDeLaConexion()
    {
        // Para cuando se ejecuta el cierre, el token ya está cancelado: saltarse la
        // limpieza dejaría al usuario «en línea» para siempre.
        _contexto.Abortar();

        await Hub().Desconectar();

        await _conexiones.Received(1).EliminarAsync("conexion-1", _reloj.Ahora, CancellationToken.None);
    }

    [Fact]
    public async Task UnaConexionQueNoConstabaNoProduceAvisos()
    {
        _conexiones
            .EliminarAsync(Arg.Any<string>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns((ConexionCerrada?)null);

        await Hub().Desconectar();

        await _notificador.DidNotReceiveWithAnyArgs().NotificarPresenciaAsync(default!);
    }

    [Fact]
    public async Task EnviarUnMensajeDevuelveElMensajePublicado()
    {
        var salaId = Guid.CreateVersion7();
        var esperado = Mensaje(salaId);

        _despachador
            .EjecutarAsync(Arg.Any<IComando<MensajeDto>>(), Arg.Any<CancellationToken>())
            .Returns(esperado);

        var resultado = await Hub().EnviarMensaje(salaId, "hola", Guid.CreateVersion7());

        Assert.Same(esperado, resultado);
    }

    [Fact]
    public async Task ElAutorDelMensajeSaleDelTokenYNoDeLoQueMandeElCliente()
    {
        var salaId = Guid.CreateVersion7();
        _despachador
            .EjecutarAsync(Arg.Any<IComando<MensajeDto>>(), Arg.Any<CancellationToken>())
            .Returns(Mensaje(salaId));

        await Hub().EnviarMensaje(salaId, "hola", Guid.CreateVersion7());

        await _despachador.Received(1).EjecutarAsync(
            Arg.Is<IComando<MensajeDto>>(c => ((ComandoEnviarMensaje)c).UsuarioId == _usuarioId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PasarseDeFrecuenciaDevuelveUnAvisoYNoPublicaNada()
    {
        _limitador.IntentarConsumirAsync(_usuarioId, Arg.Any<CancellationToken>()).Returns(false);

        var resultado = await Hub().EnviarMensaje(Guid.CreateVersion7(), "hola", Guid.CreateVersion7());

        Assert.Null(resultado);
        await _llamante.Received(1).ErrorRecibido(Arg.Is<string>(m => m.Contains("demasiado rápido", StringComparison.Ordinal)));
        await _despachador.DidNotReceiveWithAnyArgs().EjecutarAsync(Arg.Any<IComando<MensajeDto>>());
    }

    [Fact]
    public async Task UnErrorDeDominioAlEnviarSeDevuelveComoAvisoYNoComoFalloDeLaInvocacion()
    {
        // Si se propagara, SignalR cerraría la invocación con un error genérico y el
        // usuario no sabría qué ha pasado.
        _despachador
            .EjecutarAsync(Arg.Any<IComando<MensajeDto>>(), Arg.Any<CancellationToken>())
            .Returns<Task<MensajeDto>>(_ => throw new ExcepcionAutorizacion("Debes unirte a la sala."));

        var resultado = await Hub().EnviarMensaje(Guid.CreateVersion7(), "hola", Guid.CreateVersion7());

        Assert.Null(resultado);
        await _llamante.Received(1).ErrorRecibido("Debes unirte a la sala.");
    }

    [Fact]
    public async Task UnirseAUnaSalaSuscribeLaConexionYAvisaAlResto()
    {
        var sala = Sala("Equipo");
        _despachador
            .EjecutarAsync(Arg.Any<IComando<SalaDto>>(), Arg.Any<CancellationToken>())
            .Returns(sala);

        var resultado = await Hub().UnirseSala(sala.Id);

        Assert.Same(sala, resultado);
        await _grupos.Received(1).AddToGroupAsync("conexion-1", ChatHub.NombreGrupo(sala.Id), Arg.Any<CancellationToken>());
        await _conexiones.Received(1).AgregarSalaAsync("conexion-1", "Equipo", Arg.Any<CancellationToken>());
        await _otrosDelGrupo.Received(1).UsuarioUnido("Equipo", "ana");
    }

    [Fact]
    public async Task UnErrorDeDominioAlUnirseSeDevuelveComoAviso()
    {
        _despachador
            .EjecutarAsync(Arg.Any<IComando<SalaDto>>(), Arg.Any<CancellationToken>())
            .Returns<Task<SalaDto>>(_ => throw new ExcepcionAutorizacion("La sala es privada."));

        Assert.Null(await Hub().UnirseSala(Guid.CreateVersion7()));
        await _llamante.Received(1).ErrorRecibido("La sala es privada.");
    }

    [Fact]
    public async Task AbrirUnaConversacionDirectaSuscribeLaConexionAlInstante()
    {
        var sala = Sala("eva", TipoSala.Directa);
        _despachador
            .EjecutarAsync(Arg.Any<IComando<SalaDto>>(), Arg.Any<CancellationToken>())
            .Returns(sala);

        var resultado = await Hub().AbrirConversacionDirecta("eva");

        Assert.Same(sala, resultado);
        await _grupos.Received(1).AddToGroupAsync("conexion-1", ChatHub.NombreGrupo(sala.Id), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UnErrorDeDominioAlAbrirUnaDirectaSeDevuelveComoAviso()
    {
        _despachador
            .EjecutarAsync(Arg.Any<IComando<SalaDto>>(), Arg.Any<CancellationToken>())
            .Returns<Task<SalaDto>>(_ => throw new ExcepcionNoEncontrado("No existe ningún usuario llamado 'nadie'."));

        Assert.Null(await Hub().AbrirConversacionDirecta("nadie"));
        await _llamante.Received(1).ErrorRecibido("No existe ningún usuario llamado 'nadie'.");
    }

    [Fact]
    public async Task SalirDeUnaSalaAvisaAlRestoYCancelaLaSuscripcion()
    {
        var sala = Sala("Equipo");
        DevolverSalas([sala]);
        _despachador
            .EjecutarAsync(Arg.Any<IComando<ResultadoOperacionDto>>(), Arg.Any<CancellationToken>())
            .Returns(new ResultadoOperacionDto(true, "Has salido."));

        var resultado = await Hub().SalirSala(sala.Id);

        Assert.True(resultado.Exito);
        await _otrosDelGrupo.Received(1).UsuarioSalido("Equipo", "ana");
        await _grupos.Received(1).RemoveFromGroupAsync("conexion-1", ChatHub.NombreGrupo(sala.Id), Arg.Any<CancellationToken>());
        await _conexiones.Received(1).QuitarSalaAsync("conexion-1", "Equipo", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UnErrorDeDominioAlSalirSeDevuelveComoResultadoFallido()
    {
        _despachador
            .EjecutarAsync(Arg.Any<IComando<ResultadoOperacionDto>>(), Arg.Any<CancellationToken>())
            .Returns<Task<ResultadoOperacionDto>>(_ => throw new ExcepcionNoEncontrado("La sala no existe."));

        var resultado = await Hub().SalirSala(Guid.CreateVersion7());

        Assert.False(resultado.Exito);
        Assert.Equal("La sala no existe.", resultado.Mensaje);
        await _llamante.Received(1).ErrorRecibido("La sala no existe.");
    }

    [Fact]
    public async Task MarcarLeidaDevuelveUnResultadoFallidoSiElDominioSeQueja()
    {
        _despachador
            .EjecutarAsync(Arg.Any<IComando<ResultadoOperacionDto>>(), Arg.Any<CancellationToken>())
            .Returns<Task<ResultadoOperacionDto>>(_ => throw new ExcepcionAutorizacion("No eres miembro."));

        var resultado = await Hub().MarcarLeida(Guid.CreateVersion7());

        Assert.False(resultado.Exito);
        Assert.Equal("No eres miembro.", resultado.Mensaje);
    }

    [Fact]
    public async Task ListarMiembrosDevuelveVacioAnteUnErrorDeDominio()
    {
        _despachador
            .ConsultarAsync(Arg.Any<IConsulta<IReadOnlyList<MiembroSalaDto>>>(), Arg.Any<CancellationToken>())
            .Returns<Task<IReadOnlyList<MiembroSalaDto>>>(_ => throw new ExcepcionAutorizacion("No eres miembro."));

        Assert.Empty(await Hub().ListarMiembros(Guid.CreateVersion7()));
        await _llamante.Received(1).ErrorRecibido("No eres miembro.");
    }

    [Fact]
    public async Task NoSePuedeAnunciarEscrituraEnUnaSalaAjena()
    {
        // Solo se difunde en salas ya verificadas para esta conexión: sin esa condición,
        // cualquiera podría anunciarse en una conversación a la que no pertenece.
        await Hub().Escribiendo(Guid.CreateVersion7());

        await _otrosDelGrupo.DidNotReceiveWithAnyArgs().UsuarioEscribiendo(default, default!);
    }

    [Fact]
    public async Task ElAvisoDeEscrituraSeDifundeEnUnaSalaYaVerificada()
    {
        var sala = Sala("Equipo");
        var hub = Hub();

        DevolverSalas([sala]);
        await hub.Conectar();

        await hub.Escribiendo(sala.Id);

        await _otrosDelGrupo.Received(1).UsuarioEscribiendo(sala.Id, "ana");
    }

    [Fact]
    public async Task LosAvisosDeEscrituraDemasiadoSeguidosSeDescartan()
    {
        var sala = Sala("Equipo");
        var hub = Hub();

        DevolverSalas([sala]);
        await hub.Conectar();

        await hub.Escribiendo(sala.Id);
        await hub.Escribiendo(sala.Id);

        await _otrosDelGrupo.Received(1).UsuarioEscribiendo(sala.Id, "ana");

        // Pasado el intervalo mínimo, vuelve a admitirse.
        _reloj.Avanzar(TimeSpan.FromSeconds(3));
        await hub.Escribiendo(sala.Id);

        await _otrosDelGrupo.Received(2).UsuarioEscribiendo(sala.Id, "ana");
    }

    /// <summary>Programa las salas que devolverá la consulta de bandeja.</summary>
    /// <param name="salas">Salas del usuario.</param>
    private void DevolverSalas(IReadOnlyList<SalaDto> salas)
        => _despachador
            .ConsultarAsync(Arg.Any<IConsulta<IReadOnlyList<SalaDto>>>(), Arg.Any<CancellationToken>())
            .Returns(salas);

    /// <summary>Construye la proyección de una sala.</summary>
    /// <param name="nombre">Nombre visible.</param>
    /// <param name="tipo">Naturaleza de la sala.</param>
    private static SalaDto Sala(string nombre, TipoSala tipo = TipoSala.Publica)
        => new(Guid.CreateVersion7(), nombre, null, tipo, Datos.Ahora, null, 1, EsMiembro: true);

    /// <summary>Construye la proyección de un mensaje.</summary>
    /// <param name="salaId">Sala en la que se publicó.</param>
    private static MensajeDto Mensaje(Guid salaId)
        => new(Guid.CreateVersion7(), salaId, "General", Guid.CreateVersion7(), "ana", "hola", Datos.Ahora);

    /// <summary>
    /// Contexto de conexión mínimo, con la identidad ya autenticada que el hub espera
    /// encontrar en los claims.
    /// </summary>
    private sealed class ContextoDeConexion : HubCallerContext
    {
        private readonly CancellationTokenSource _cancelacion = new();

        /// <summary>Crea el contexto.</summary>
        /// <param name="conexionId">Identificador de conexión.</param>
        /// <param name="usuarioId">Usuario autenticado.</param>
        /// <param name="nombreUsuario">Nombre del usuario.</param>
        public ContextoDeConexion(string conexionId, Guid usuarioId, string nombreUsuario)
        {
            ConnectionId = conexionId;
            UserIdentifier = usuarioId.ToString();

            User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(JwtRegisteredClaimNames.Sub, usuarioId.ToString()),
                    new Claim(ClaimTypes.Name, nombreUsuario)
                ],
                "Prueba",
                ClaimTypes.Name,
                ClaimTypes.Role));
        }

        /// <inheritdoc />
        public override string ConnectionId { get; }

        /// <inheritdoc />
        public override string? UserIdentifier { get; }

        /// <inheritdoc />
        public override ClaimsPrincipal? User { get; }

        /// <inheritdoc />
        public override IDictionary<object, object?> Items { get; } = new Dictionary<object, object?>();

        /// <inheritdoc />
        public override IFeatureCollection Features { get; } = new FeatureCollection();

        /// <inheritdoc />
        public override CancellationToken ConnectionAborted => _cancelacion.Token;

        /// <inheritdoc />
        public override void Abort() => _cancelacion.Cancel();

        /// <summary>Cancela el token de la conexión, como haría un cierre abrupto.</summary>
        public void Abortar() => _cancelacion.Cancel();
    }
}
