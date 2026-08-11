using System.Globalization;
using Chat.Aplicacion.Abstracciones;
using Chat.Aplicacion.Comandos.Mensajes;
using Chat.Aplicacion.Comandos.Salas;
using Chat.Aplicacion.Consultas.Salas;
using Chat.Aplicacion.Consultas.Usuarios;
using Chat.Aplicacion.Cqrs;
using Chat.Aplicacion.Dtos;
using Chat.Dominio.Excepciones;
using Chat.Servidor.Configuracion;
using Chat.Servidor.Observabilidad;
using Chat.Servidor.Servicios;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Chat.Servidor.Hubs;

/// <summary>
/// Concentrador de mensajería en tiempo real. Todas sus invocaciones exigen un
/// token JWT válido; la identidad se toma siempre de los claims, nunca de los
/// parámetros enviados por el cliente.
/// </summary>
[Authorize(Policy = ExtensionesAutenticacion.PoliticaUsuarioAutenticado)]
public sealed class ChatHub : Hub<IClienteChat>
{
    /// <summary>Clave con la que se guardan en la conexión las salas ya verificadas.</summary>
    private const string ClaveSalasVerificadas = "salas-verificadas";

    /// <summary>Clave con la que se guarda el instante del último aviso de escritura.</summary>
    private const string ClaveUltimaEscritura = "ultima-escritura";

    /// <summary>Intervalo mínimo entre dos avisos de «está escribiendo» de una misma conexión.</summary>
    private static readonly TimeSpan IntervaloMinimoEscritura = TimeSpan.FromSeconds(2);

    private readonly IDespachador _despachador;
    private readonly IRegistroConexiones _conexiones;
    private readonly INotificadorTiempoReal _notificador;
    private readonly ILimitadorEnvios _limitador;
    private readonly IProveedorFechaHora _reloj;
    private readonly ILogger<ChatHub> _registro;

    /// <summary>Crea el hub.</summary>
    public ChatHub(
        IDespachador despachador,
        IRegistroConexiones conexiones,
        INotificadorTiempoReal notificador,
        ILimitadorEnvios limitador,
        IProveedorFechaHora reloj,
        ILogger<ChatHub> registro)
    {
        _despachador = despachador;
        _conexiones = conexiones;
        _notificador = notificador;
        _limitador = limitador;
        _reloj = reloj;
        _registro = registro;
    }

    /// <summary>Calcula el nombre del grupo de SignalR asociado a una sala.</summary>
    /// <param name="salaId">Identificador de la sala.</param>
    public static string NombreGrupo(Guid salaId)
        => string.Create(CultureInfo.InvariantCulture, $"sala:{salaId}");

    /// <inheritdoc />
    public override async Task OnConnectedAsync()
    {
        await Conectar().ConfigureAwait(false);
        await base.OnConnectedAsync().ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override async Task OnDisconnectedAsync(Exception? excepcion)
    {
        await Desconectar(excepcion).ConfigureAwait(false);
        await base.OnDisconnectedAsync(excepcion).ConfigureAwait(false);
    }

    /// <summary>
    /// Registra la conexión, la suscribe automáticamente a los grupos de todas sus
    /// salas y conversaciones, devuelve al cliente el estado inicial y anuncia su
    /// presencia si es la primera conexión del usuario.
    /// </summary>
    public async Task Conectar()
    {
        var usuarioId = Context.User!.ObtenerUsuarioId();
        var nombreUsuario = Context.User!.ObtenerNombreUsuario();
        var ahora = _reloj.Ahora;

        using var actividad = MedidasChat.Fuente.StartActivity("hub.conectar");
        actividad?.SetTag("chat.usuario_id", usuarioId);

        var esPrimeraConexion = await _conexiones
            .RegistrarAsync(Context.ConnectionId, usuarioId, nombreUsuario, ahora, Context.ConnectionAborted)
            .ConfigureAwait(false);

        var salasUsuario = await ObtenerSalasDelUsuarioAsync(usuarioId).ConfigureAwait(false);
        var verificadas = SalasVerificadas();

        foreach (var sala in salasUsuario)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, NombreGrupo(sala.Id), Context.ConnectionAborted)
                .ConfigureAwait(false);

            await _conexiones
                .AgregarSalaAsync(Context.ConnectionId, sala.Nombre, Context.ConnectionAborted)
                .ConfigureAwait(false);

            verificadas.Add(sala.Id);
        }

        MedidasChat.RegistrarConexion(esPrimeraConexion);

        _registro.LogInformation(
            "Conexión SignalR establecida. ConexionId={ConexionId} UsuarioId={UsuarioId} NombreUsuario={NombreUsuario} Salas={TotalSalas}",
            Context.ConnectionId,
            usuarioId,
            nombreUsuario,
            salasUsuario.Count);

        await Clients.Caller.Conectado(nombreUsuario, salasUsuario).ConfigureAwait(false);

        if (esPrimeraConexion)
        {
            await _notificador
                .NotificarPresenciaAsync(new PresenciaDto(usuarioId, nombreUsuario, true, ahora, 1))
                .ConfigureAwait(false);
        }
    }

    /// <summary>Cierra la sesión en tiempo real y libera el estado asociado a la conexión.</summary>
    /// <param name="excepcion">Excepción que provocó el cierre, si la hubo.</param>
    public async Task Desconectar(Exception? excepcion = null)
    {
        var ahora = _reloj.Ahora;

        // El cierre no usa el token de la conexión: para cuando se ejecuta ya está
        // cancelado, y saltarse la limpieza dejaría al usuario «en línea» para siempre.
        var cerrada = await _conexiones
            .EliminarAsync(Context.ConnectionId, ahora, CancellationToken.None)
            .ConfigureAwait(false);

        if (excepcion is null)
        {
            _registro.LogInformation(
                "Conexión SignalR cerrada. ConexionId={ConexionId} UsuarioId={UsuarioId}",
                Context.ConnectionId,
                cerrada?.UsuarioId);
        }
        else
        {
            _registro.LogWarning(
                excepcion,
                "Conexión SignalR cerrada por error. ConexionId={ConexionId} UsuarioId={UsuarioId}",
                Context.ConnectionId,
                cerrada?.UsuarioId);
        }

        if (cerrada is null)
        {
            return;
        }

        MedidasChat.RegistrarDesconexion(cerrada.FueLaUltima);

        if (cerrada.FueLaUltima)
        {
            await _notificador
                .NotificarPresenciaAsync(new PresenciaDto(cerrada.UsuarioId, cerrada.NombreUsuario, false, ahora, 0))
                .ConfigureAwait(false);
        }
    }

    /// <summary>Publica un mensaje en una sala y lo difunde a sus miembros conectados.</summary>
    /// <param name="salaId">Sala destino.</param>
    /// <param name="texto">Contenido en claro; puede ir vacío si se adjunta una imagen.</param>
    /// <param name="identificadorEnvio">Identificador único generado por el cliente (antirrepetición).</param>
    /// <param name="adjuntoId">
    /// Imagen subida previamente por HTTP. Por el hub viaja solo el identificador:
    /// los bytes ya están en el servidor.
    /// </param>
    public async Task<MensajeDto?> EnviarMensaje(
        Guid salaId,
        string texto,
        Guid identificadorEnvio,
        Guid? adjuntoId = null)
    {
        var usuarioId = Context.User!.ObtenerUsuarioId();

        using var actividad = MedidasChat.Fuente.StartActivity("hub.enviar_mensaje");
        actividad?.SetTag("chat.sala_id", salaId);
        actividad?.SetTag("chat.usuario_id", usuarioId);

        if (!await _limitador.IntentarConsumirAsync(usuarioId, Context.ConnectionAborted).ConfigureAwait(false))
        {
            MedidasChat.RegistrarMensajeRechazado("limite_frecuencia");

            _registro.LogWarning(
                "Envío rechazado por límite de frecuencia. UsuarioId={UsuarioId} SalaId={SalaId}",
                usuarioId,
                salaId);

            await Clients.Caller
                .ErrorRecibido("Estás enviando mensajes demasiado rápido. Espera unos segundos.")
                .ConfigureAwait(false);

            return null;
        }

        try
        {
            var comando = new ComandoEnviarMensaje(
                usuarioId,
                new SolicitudEnviarMensajeDto(salaId, texto, identificadorEnvio, adjuntoId));

            // El manejador cifra, persiste y difunde el mensaje a través del notificador.
            var mensaje = await _despachador.EjecutarAsync(comando, Context.ConnectionAborted).ConfigureAwait(false);

            MedidasChat.RegistrarMensajeEnviado(mensaje.Texto.Length);
            return mensaje;
        }
        catch (ExcepcionDominio excepcion)
        {
            MedidasChat.RegistrarMensajeRechazado("dominio");
            actividad?.SetTag("chat.error", excepcion.GetType().Name);

            await Clients.Caller.ErrorRecibido(excepcion.Message).ConfigureAwait(false);
            return null;
        }
    }

    /// <summary>Une al usuario a una sala pública y suscribe la conexión a su grupo.</summary>
    /// <param name="salaId">Sala destino.</param>
    public async Task<SalaDto?> UnirseSala(Guid salaId)
    {
        var usuarioId = Context.User!.ObtenerUsuarioId();
        var nombreUsuario = Context.User!.ObtenerNombreUsuario();

        try
        {
            var sala = await _despachador
                .EjecutarAsync(new ComandoUnirseSala(salaId, usuarioId), Context.ConnectionAborted)
                .ConfigureAwait(false);

            await SuscribirAsync(sala).ConfigureAwait(false);

            await Clients.OthersInGroup(NombreGrupo(salaId))
                .UsuarioUnido(sala.Nombre, nombreUsuario)
                .ConfigureAwait(false);

            return sala;
        }
        catch (ExcepcionDominio excepcion)
        {
            await Clients.Caller.ErrorRecibido(excepcion.Message).ConfigureAwait(false);
            return null;
        }
    }

    /// <summary>
    /// Abre (o recupera) la conversación privada con otra persona y suscribe la
    /// conexión a su grupo para recibir los mensajes al instante.
    /// </summary>
    /// <param name="nombreUsuario">Nombre del interlocutor.</param>
    public async Task<SalaDto?> AbrirConversacionDirecta(string nombreUsuario)
    {
        var usuarioId = Context.User!.ObtenerUsuarioId();

        using var actividad = MedidasChat.Fuente.StartActivity("hub.abrir_directa");
        actividad?.SetTag("chat.usuario_id", usuarioId);

        try
        {
            var comando = new ComandoAbrirConversacionDirecta(
                usuarioId,
                new SolicitudConversacionDirectaDto(nombreUsuario));

            var sala = await _despachador.EjecutarAsync(comando, Context.ConnectionAborted).ConfigureAwait(false);

            await SuscribirAsync(sala).ConfigureAwait(false);
            return sala;
        }
        catch (ExcepcionDominio excepcion)
        {
            await Clients.Caller.ErrorRecibido(excepcion.Message).ConfigureAwait(false);
            return null;
        }
    }

    /// <summary>Saca al usuario de una sala y cancela la suscripción de la conexión.</summary>
    /// <param name="salaId">Sala de origen.</param>
    public async Task<ResultadoOperacionDto> SalirSala(Guid salaId)
    {
        var usuarioId = Context.User!.ObtenerUsuarioId();
        var nombreUsuario = Context.User!.ObtenerNombreUsuario();

        try
        {
            var salas = await ObtenerSalasDelUsuarioAsync(usuarioId).ConfigureAwait(false);
            var nombreSala = salas.FirstOrDefault(s => s.Id == salaId)?.Nombre ?? salaId.ToString();

            var resultado = await _despachador
                .EjecutarAsync(new ComandoSalirSala(salaId, usuarioId), Context.ConnectionAborted)
                .ConfigureAwait(false);

            await Clients.OthersInGroup(NombreGrupo(salaId))
                .UsuarioSalido(nombreSala, nombreUsuario)
                .ConfigureAwait(false);

            await Groups.RemoveFromGroupAsync(Context.ConnectionId, NombreGrupo(salaId), Context.ConnectionAborted)
                .ConfigureAwait(false);

            await _conexiones
                .QuitarSalaAsync(Context.ConnectionId, nombreSala, Context.ConnectionAborted)
                .ConfigureAwait(false);

            SalasVerificadas().Remove(salaId);

            return resultado;
        }
        catch (ExcepcionDominio excepcion)
        {
            await Clients.Caller.ErrorRecibido(excepcion.Message).ConfigureAwait(false);
            return new ResultadoOperacionDto(false, excepcion.Message);
        }
    }

    /// <summary>Devuelve las salas y conversaciones del usuario, con sus mensajes pendientes.</summary>
    public Task<IReadOnlyList<SalaDto>> ListarSalas()
        => ObtenerSalasDelUsuarioAsync(Context.User!.ObtenerUsuarioId());

    /// <summary>Devuelve los miembros de una sala con su estado de conexión.</summary>
    /// <param name="salaId">Sala consultada.</param>
    public async Task<IReadOnlyList<MiembroSalaDto>> ListarMiembros(Guid salaId)
    {
        try
        {
            return await _despachador
                .ConsultarAsync(
                    new ConsultaMiembrosSala(salaId, Context.User!.ObtenerUsuarioId()),
                    Context.ConnectionAborted)
                .ConfigureAwait(false);
        }
        catch (ExcepcionDominio excepcion)
        {
            await Clients.Caller.ErrorRecibido(excepcion.Message).ConfigureAwait(false);
            return [];
        }
    }

    /// <summary>Devuelve el estado de conexión de los usuarios conocidos.</summary>
    public Task<IReadOnlyList<PresenciaDto>> ListarPresencia()
        => _despachador.ConsultarAsync(new ConsultaPresencia(), Context.ConnectionAborted);

    /// <summary>Deja la conversación al día, poniendo a cero sus mensajes pendientes.</summary>
    /// <param name="salaId">Sala leída.</param>
    public async Task<ResultadoOperacionDto> MarcarLeida(Guid salaId)
    {
        try
        {
            return await _despachador
                .EjecutarAsync(
                    new ComandoMarcarSalaLeida(salaId, Context.User!.ObtenerUsuarioId()),
                    Context.ConnectionAborted)
                .ConfigureAwait(false);
        }
        catch (ExcepcionDominio excepcion)
        {
            return new ResultadoOperacionDto(false, excepcion.Message);
        }
    }

    /// <summary>
    /// Avisa al resto de la sala de que el usuario está escribiendo. No se persiste
    /// nada: es una señal efímera y se descarta si llega demasiado seguida.
    /// </summary>
    /// <param name="salaId">Sala en la que se escribe.</param>
    public async Task Escribiendo(Guid salaId)
    {
        if (!EsMomentoDeAvisar())
        {
            return;
        }

        // Solo se difunde en salas ya verificadas para esta conexión, de modo que
        // nadie pueda anunciarse en una conversación a la que no pertenece.
        if (!SalasVerificadas().Contains(salaId))
        {
            return;
        }

        await Clients.OthersInGroup(NombreGrupo(salaId))
            .UsuarioEscribiendo(salaId, Context.User!.ObtenerNombreUsuario())
            .ConfigureAwait(false);
    }

    /// <summary>Suscribe la conexión actual al grupo de una sala y la da por verificada.</summary>
    /// <param name="sala">Sala a la que el usuario acaba de acceder.</param>
    private async Task SuscribirAsync(SalaDto sala)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, NombreGrupo(sala.Id), Context.ConnectionAborted)
            .ConfigureAwait(false);

        await _conexiones
            .AgregarSalaAsync(Context.ConnectionId, sala.Nombre, Context.ConnectionAborted)
            .ConfigureAwait(false);

        SalasVerificadas().Add(sala.Id);
    }

    /// <summary>
    /// Conjunto de salas cuya pertenencia ya se comprobó para esta conexión. Se guarda
    /// en el estado de la conexión, que SignalR libera al cerrarse.
    /// </summary>
    private HashSet<Guid> SalasVerificadas()
    {
        if (Context.Items.TryGetValue(ClaveSalasVerificadas, out var valor) && valor is HashSet<Guid> conjunto)
        {
            return conjunto;
        }

        var nuevo = new HashSet<Guid>();
        Context.Items[ClaveSalasVerificadas] = nuevo;
        return nuevo;
    }

    /// <summary>Limita la frecuencia de los avisos de escritura de una misma conexión.</summary>
    /// <returns><c>true</c> si ha pasado el intervalo mínimo desde el último aviso.</returns>
    private bool EsMomentoDeAvisar()
    {
        var ahora = _reloj.Ahora;

        if (Context.Items.TryGetValue(ClaveUltimaEscritura, out var valor)
            && valor is DateTimeOffset ultima
            && ahora - ultima < IntervaloMinimoEscritura)
        {
            return false;
        }

        Context.Items[ClaveUltimaEscritura] = ahora;
        return true;
    }

    /// <summary>Obtiene las salas y conversaciones a las que pertenece el usuario.</summary>
    /// <param name="usuarioId">Usuario consultado.</param>
    private Task<IReadOnlyList<SalaDto>> ObtenerSalasDelUsuarioAsync(Guid usuarioId)
        => _despachador.ConsultarAsync(new ConsultaSalasDeUsuario(usuarioId), Context.ConnectionAborted);
}
