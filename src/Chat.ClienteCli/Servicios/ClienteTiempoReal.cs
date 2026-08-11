using Chat.Aplicacion.Dtos;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Options;

namespace Chat.ClienteCli.Servicios;

/// <summary>
/// Conexión SignalR del cliente. Encapsula el ciclo de vida del hub, la
/// reconexión automática y la suscripción a los eventos del servidor.
/// </summary>
public sealed class ClienteTiempoReal : IAsyncDisposable, IDisposable
{
    private readonly ClienteApi _api;
    private readonly OpcionesCliente _opciones;
    private HubConnection? _conexion;

    /// <summary>Crea el cliente de tiempo real.</summary>
    /// <param name="api">Cliente de la API, usado para obtener el token vigente.</param>
    /// <param name="opciones">Configuración del cliente.</param>
    public ClienteTiempoReal(ClienteApi api, IOptions<OpcionesCliente> opciones)
    {
        _api = api;
        _opciones = opciones.Value;
    }

    /// <summary>Se dispara al recibir un mensaje nuevo.</summary>
    public event Action<MensajeDto>? MensajeRecibido;

    /// <summary>Se dispara cuando un usuario entra en una sala.</summary>
    public event Action<string, string>? UsuarioUnido;

    /// <summary>Se dispara cuando un usuario abandona una sala.</summary>
    public event Action<string, string>? UsuarioSalido;

    /// <summary>Se dispara cuando alguien abre una conversación con el usuario o le invita a una sala.</summary>
    public event Action<SalaDto>? SalaDisponible;

    /// <summary>Se dispara cuando otro usuario se conecta o se desconecta.</summary>
    public event Action<PresenciaDto>? PresenciaCambiada;

    /// <summary>Se dispara cuando alguien está escribiendo en una sala.</summary>
    public event Action<Guid, string>? UsuarioEscribiendo;

    /// <summary>Se dispara cuando el servidor comunica un error recuperable.</summary>
    public event Action<string>? ErrorRecibido;

    /// <summary>Se dispara cuando cambia el estado de la conexión.</summary>
    public event Action<string>? EstadoCambiado;

    /// <summary>Indica si la conexión está establecida.</summary>
    public bool Conectado => _conexion?.State == HubConnectionState.Connected;

    /// <summary>Abre la conexión con el hub y registra los manejadores de eventos.</summary>
    /// <param name="rutaHub">Ruta relativa del hub publicada por el servidor.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    public async Task ConectarAsync(string rutaHub, CancellationToken cancelacion = default)
    {
        if (_conexion is not null)
        {
            return;
        }

        var direccion = new Uri(new Uri(_opciones.UrlServidor), rutaHub);

        _conexion = new HubConnectionBuilder()
            .WithUrl(direccion, opciones =>
            {
                // El token se resuelve en cada (re)conexión, de modo que una sesión
                // renovada se aplica sin reiniciar el cliente.
                opciones.AccessTokenProvider = async () =>
                    await _api.ObtenerTokenVigenteAsync(CancellationToken.None).ConfigureAwait(false);

                if (_opciones.AceptarCertificadosNoConfiables)
                {
                    opciones.HttpMessageHandlerFactory = _ => new HttpClientHandler
                    {
                        ServerCertificateCustomValidationCallback =
                            HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                    };
                }
            })
            .WithAutomaticReconnect([TimeSpan.Zero, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10)])
            .Build();

        _conexion.On<MensajeDto>("RecibirMensaje", mensaje => MensajeRecibido?.Invoke(mensaje));
        _conexion.On<string, string>("UsuarioUnido", (sala, usuario) => UsuarioUnido?.Invoke(sala, usuario));
        _conexion.On<string, string>("UsuarioSalido", (sala, usuario) => UsuarioSalido?.Invoke(sala, usuario));
        _conexion.On<SalaDto>("SalaDisponible", sala => SalaDisponible?.Invoke(sala));
        _conexion.On<PresenciaDto>("PresenciaCambiada", presencia => PresenciaCambiada?.Invoke(presencia));
        _conexion.On<Guid, string>("UsuarioEscribiendo", (sala, usuario) => UsuarioEscribiendo?.Invoke(sala, usuario));
        _conexion.On<string>("ErrorRecibido", mensaje => ErrorRecibido?.Invoke(mensaje));
        _conexion.On<SalaDto>("SalaCreada", _ => { });
        _conexion.On<string, IReadOnlyList<SalaDto>>("Conectado", (_, _) => { });

        _conexion.Reconnecting += _ =>
        {
            EstadoCambiado?.Invoke("reconectando");
            return Task.CompletedTask;
        };

        _conexion.Reconnected += _ =>
        {
            EstadoCambiado?.Invoke("reconectado");
            return Task.CompletedTask;
        };

        _conexion.Closed += _ =>
        {
            EstadoCambiado?.Invoke("desconectado");
            return Task.CompletedTask;
        };

        await _conexion.StartAsync(cancelacion).ConfigureAwait(false);
        EstadoCambiado?.Invoke("conectado");
    }

    /// <summary>Envía un mensaje a través del hub.</summary>
    /// <param name="salaId">Sala destino.</param>
    /// <param name="texto">Contenido en claro; puede ir vacío si se adjunta una imagen.</param>
    /// <param name="adjuntoId">Imagen ya subida por HTTP que acompaña al mensaje.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    /// <returns>El mensaje persistido, o <c>null</c> si el servidor lo rechazó.</returns>
    public Task<MensajeDto?> EnviarMensajeAsync(
        Guid salaId,
        string texto,
        Guid? adjuntoId = null,
        CancellationToken cancelacion = default)
        => RequerirConexion().InvokeAsync<MensajeDto?>(
            "EnviarMensaje",
            salaId,
            texto,
            // Identificador único por envío: hace la operación idempotente y
            // permite al servidor descartar reenvíos repetidos.
            Guid.CreateVersion7(),
            adjuntoId,
            cancelacion);

    /// <summary>Une al usuario a una sala mediante el hub.</summary>
    /// <param name="salaId">Sala destino.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    public Task<SalaDto?> UnirseSalaAsync(Guid salaId, CancellationToken cancelacion = default)
        => RequerirConexion().InvokeAsync<SalaDto?>("UnirseSala", salaId, cancelacion);

    /// <summary>Abre o recupera la conversación directa con otra persona.</summary>
    /// <param name="nombreUsuario">Nombre del interlocutor.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    public Task<SalaDto?> AbrirConversacionDirectaAsync(string nombreUsuario, CancellationToken cancelacion = default)
        => RequerirConexion().InvokeAsync<SalaDto?>("AbrirConversacionDirecta", nombreUsuario, cancelacion);

    /// <summary>Saca al usuario de una sala mediante el hub.</summary>
    /// <param name="salaId">Sala de origen.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    public Task<ResultadoOperacionDto> SalirSalaAsync(Guid salaId, CancellationToken cancelacion = default)
        => RequerirConexion().InvokeAsync<ResultadoOperacionDto>("SalirSala", salaId, cancelacion);

    /// <summary>Obtiene los miembros de una sala con su estado de conexión.</summary>
    /// <param name="salaId">Sala consultada.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    public Task<IReadOnlyList<MiembroSalaDto>> ListarMiembrosAsync(
        Guid salaId,
        CancellationToken cancelacion = default)
        => RequerirConexion().InvokeAsync<IReadOnlyList<MiembroSalaDto>>("ListarMiembros", salaId, cancelacion);

    /// <summary>Obtiene las salas y conversaciones del usuario con sus mensajes pendientes.</summary>
    /// <param name="cancelacion">Token de cancelación.</param>
    public Task<IReadOnlyList<SalaDto>> ListarSalasAsync(CancellationToken cancelacion = default)
        => RequerirConexion().InvokeAsync<IReadOnlyList<SalaDto>>("ListarSalas", cancelacion);

    /// <summary>Deja la conversación al día en el servidor.</summary>
    /// <param name="salaId">Sala leída.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    public Task<ResultadoOperacionDto> MarcarLeidaAsync(Guid salaId, CancellationToken cancelacion = default)
        => RequerirConexion().InvokeAsync<ResultadoOperacionDto>("MarcarLeida", salaId, cancelacion);

    /// <summary>Avisa al resto de la sala de que el usuario está escribiendo.</summary>
    /// <param name="salaId">Sala en la que se escribe.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    public Task AvisarEscribiendoAsync(Guid salaId, CancellationToken cancelacion = default)
        => RequerirConexion().SendAsync("Escribiendo", salaId, cancelacion);

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_conexion is not null)
        {
            await _conexion.DisposeAsync().ConfigureAwait(false);
            _conexion = null;
        }
    }

    /// <summary>
    /// Liberación síncrona. El contenedor de dependencias la invoca al cerrarse, y
    /// exige que un servicio registrado como singleton la implemente además de la
    /// versión asíncrona.
    /// </summary>
    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();

    /// <summary>Devuelve la conexión activa o lanza si aún no se ha establecido.</summary>
    private HubConnection RequerirConexion()
        => _conexion ?? throw new InvalidOperationException("La conexión con el servidor no está establecida.");
}
