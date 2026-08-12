using Chat.Aplicacion.Abstracciones;
using Chat.Aplicacion.Dtos;
using Chat.Dominio.Entidades;
using Chat.Servidor.Hubs;
using Chat.Servidor.Servicios;
using Chat.Tests.Comun;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Chat.Tests.Servidor;

/// <summary>
/// Pruebas del notificador en tiempo real, la única pieza que conoce el contexto del
/// hub. Lo que se comprueba es a quién se dirige cada aviso: un mensaje va al grupo de
/// su sala y una sala nueva va solo a su destinatario.
/// </summary>
public sealed class PruebasNotificadorSignalR
{
    private readonly IHubContext<ChatHub, IClienteChat> _hub = Substitute.For<IHubContext<ChatHub, IClienteChat>>();
    private readonly IHubClients<IClienteChat> _clientes = Substitute.For<IHubClients<IClienteChat>>();
    private readonly IGroupManager _grupos = Substitute.For<IGroupManager>();
    private readonly IRegistroConexiones _conexiones = Substitute.For<IRegistroConexiones>();

    private readonly IClienteChat _grupo = Substitute.For<IClienteChat>();
    private readonly IClienteChat _todos = Substitute.For<IClienteChat>();
    private readonly IClienteChat _usuario = Substitute.For<IClienteChat>();

    public PruebasNotificadorSignalR()
    {
        _hub.Clients.Returns(_clientes);
        _hub.Groups.Returns(_grupos);

        _clientes.Group(Arg.Any<string>()).Returns(_grupo);
        _clientes.All.Returns(_todos);
        _clientes.User(Arg.Any<string>()).Returns(_usuario);

        _conexiones
            .ConexionesDeAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<string>>(_ => []);
    }

    private NotificadorSignalR Notificador => new(_hub, _conexiones);

    [Fact]
    public async Task UnMensajeSoloLlegaAlGrupoDeSuSala()
    {
        var salaId = Guid.CreateVersion7();
        var mensaje = new MensajeDto(
            Guid.CreateVersion7(), salaId, "General", Guid.CreateVersion7(), "ana", "hola", Datos.Ahora);

        await Notificador.NotificarMensajeAsync(mensaje);

        _clientes.Received(1).Group(ChatHub.NombreGrupo(salaId));
        await _grupo.Received(1).RecibirMensaje(mensaje);
        await _todos.DidNotReceiveWithAnyArgs().RecibirMensaje(default!);
    }

    [Fact]
    public async Task LasAltasYBajasDeSalaSeDifundenATodos()
    {
        await Notificador.NotificarUsuarioUnidoAsync("General", "ana");
        await Notificador.NotificarUsuarioSalidoAsync("General", "eva");

        await _todos.Received(1).UsuarioUnido("General", "ana");
        await _todos.Received(1).UsuarioSalido("General", "eva");
    }

    [Fact]
    public async Task UnaSalaPublicaNuevaSeAnunciaATodos()
    {
        var sala = Sala();

        await Notificador.NotificarSalaCreadaAsync(sala);

        await _todos.Received(1).SalaCreada(sala);
    }

    [Fact]
    public async Task LaPresenciaSeDifundeATodos()
    {
        var presencia = new PresenciaDto(Guid.CreateVersion7(), "ana", true, Datos.Ahora, 1);

        await Notificador.NotificarPresenciaAsync(presencia);

        await _todos.Received(1).PresenciaCambiada(presencia);
    }

    [Fact]
    public async Task UnaSalaDisponibleSoloLlegaASuDestinatario()
    {
        var destinatario = Guid.CreateVersion7();
        var sala = Sala();

        await Notificador.NotificarSalaDisponibleAsync(destinatario, sala);

        _clientes.Received(1).User(destinatario.ToString());
        await _usuario.Received(1).SalaDisponible(sala);
        await _todos.DidNotReceiveWithAnyArgs().SalaDisponible(default!);
    }

    [Fact]
    public async Task LasConexionesAbiertasDelDestinatarioSeSuscribenALaSalaNueva()
    {
        // Sin esto, quien ya estaba conectado no recibiría los mensajes de la sala hasta
        // reconectarse: su conexión se suscribió a los grupos que existían al entrar.
        var destinatario = Guid.CreateVersion7();
        var sala = Sala();

        _conexiones
            .ConexionesDeAsync(destinatario, Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<string>>(_ => ["c1", "c2"]);

        await Notificador.NotificarSalaDisponibleAsync(destinatario, sala);

        await _grupos.Received(1).AddToGroupAsync("c1", ChatHub.NombreGrupo(sala.Id), Arg.Any<CancellationToken>());
        await _grupos.Received(1).AddToGroupAsync("c2", ChatHub.NombreGrupo(sala.Id), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LosArgumentosNulosSeRechazan()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => Notificador.NotificarMensajeAsync(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => Notificador.NotificarSalaCreadaAsync(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => Notificador.NotificarPresenciaAsync(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => Notificador.NotificarSalaDisponibleAsync(Guid.CreateVersion7(), null!));
    }

    /// <summary>Construye la proyección de una sala.</summary>
    private static SalaDto Sala()
        => new(Guid.CreateVersion7(), "Equipo", null, TipoSala.Publica, Datos.Ahora, null, 1, EsMiembro: true);
}

/// <summary>
/// Pruebas del servicio de latido, que es la red de seguridad frente a una réplica que
/// se cae de golpe y deja sus conexiones registradas.
/// </summary>
public sealed class PruebasServicioLatidoReplica
{
    private readonly IRegistroConexiones _conexiones = Substitute.For<IRegistroConexiones>();
    private readonly INotificadorTiempoReal _notificador = Substitute.For<INotificadorTiempoReal>();

    [Fact]
    public async Task ElPrimerLatidoSaleNadaMasArrancar()
    {
        _conexiones
            .LatirYLimpiarAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<PresenciaDto>>(_ => []);

        using var servicio = Construir();
        await servicio.StartAsync(CancellationToken.None);

        await EsperarAsync(async () =>
        {
            await _conexiones.Received(1).LatirYLimpiarAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
        });

        await servicio.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task LasPresenciasLiberadasPorUnaReplicaCaidaSeAnuncian()
    {
        // Sin este aviso, esos usuarios seguirían apareciendo «en línea» en todos los
        // clientes hasta que alguno recargase.
        var caido = new PresenciaDto(Guid.CreateVersion7(), "ana", false, Datos.Ahora, 0);

        _conexiones
            .LatirYLimpiarAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<PresenciaDto>>(_ => [caido]);

        using var servicio = Construir();
        await servicio.StartAsync(CancellationToken.None);

        await EsperarAsync(async () =>
        {
            await _notificador.Received(1).NotificarPresenciaAsync(caido, Arg.Any<CancellationToken>());
        });

        await servicio.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task PerderUnLatidoNoTumbaElServicio()
    {
        // El margen tolera varios latidos seguidos perdidos: un fallo puntual del
        // almacén compartido no puede llevarse por delante la tarea periódica.
        _conexiones
            .LatirYLimpiarAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns<Task<IReadOnlyList<PresenciaDto>>>(_ => throw new TimeoutException("Valkey no responde"));

        using var servicio = Construir();
        await servicio.StartAsync(CancellationToken.None);

        await EsperarAsync(async () =>
        {
            await _conexiones.Received(1).LatirYLimpiarAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
        });

        // El servicio sigue en pie y se apaga con normalidad.
        await servicio.StopAsync(CancellationToken.None);
    }

    /// <summary>Monta el servicio con un contenedor que resuelve sus dependencias.</summary>
    private ServicioLatidoReplica Construir()
    {
        var servicios = new ServiceCollection();
        servicios.AddScoped(_ => _conexiones);
        servicios.AddScoped(_ => _notificador);

        return new ServicioLatidoReplica(
            servicios.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
            NullLogger<ServicioLatidoReplica>.Instance);
    }

    /// <summary>
    /// Espera a que una comprobación se cumpla. El servicio corre en segundo plano, así
    /// que hay que darle margen sin quedarse esperando indefinidamente si algo falla.
    /// </summary>
    /// <param name="comprobacion">Aserción a satisfacer.</param>
    private static async Task EsperarAsync(Func<Task> comprobacion)
    {
        var limite = DateTime.UtcNow.AddSeconds(5);

        while (true)
        {
            try
            {
                await comprobacion();
                return;
            }
            catch when (DateTime.UtcNow < limite)
            {
                await Task.Delay(25);
            }
        }
    }
}
