using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Chat.Aplicacion.Abstracciones;
using Chat.Aplicacion.Dtos;
using Microsoft.Extensions.Options;

namespace Chat.ClienteCli.Servicios;

/// <summary>
/// Cliente HTTP de la API de dotChat. Gestiona la sesión de forma transparente:
/// adjunta el token de acceso, lo renueva con el token de refresco cuando caduca y
/// persiste la sesión resultante.
/// </summary>
public sealed class ClienteApi
{
    private static readonly JsonSerializerOptions OpcionesJson = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly AlmacenSesion _almacen;
    private readonly OpcionesCliente _opciones;
    private readonly SemaphoreSlim _cerrojoRefresco = new(1, 1);

    private SesionAlmacenada? _sesion;

    /// <summary>Crea el cliente.</summary>
    /// <param name="http">Cliente HTTP configurado con la dirección base.</param>
    /// <param name="almacen">Almacén de sesión local.</param>
    /// <param name="opciones">Configuración del cliente.</param>
    public ClienteApi(HttpClient http, AlmacenSesion almacen, IOptions<OpcionesCliente> opciones)
    {
        _http = http;
        _almacen = almacen;
        _opciones = opciones.Value;
    }

    /// <summary>Dirección base del servidor.</summary>
    public string UrlServidor => _opciones.UrlServidor;

    /// <summary>Sesión activa, si el usuario ya ha iniciado sesión.</summary>
    public SesionAlmacenada? Sesion => _sesion;

    /// <summary>Carga la sesión almacenada en disco, si la hay.</summary>
    /// <param name="cancelacion">Token de cancelación.</param>
    /// <returns>La sesión cargada o <c>null</c>.</returns>
    public async Task<SesionAlmacenada?> CargarSesionAsync(CancellationToken cancelacion = default)
    {
        _sesion = await _almacen.LeerAsync(cancelacion).ConfigureAwait(false);

        // Una sesión de otro servidor no sirve para este.
        if (_sesion is not null && !string.Equals(_sesion.UrlServidor, _opciones.UrlServidor, StringComparison.OrdinalIgnoreCase))
        {
            _sesion = null;
        }

        return _sesion;
    }

    /// <summary>Obtiene la sesión activa o lanza si no hay ninguna.</summary>
    /// <param name="cancelacion">Token de cancelación.</param>
    /// <exception cref="ExcepcionApi">Si no hay sesión iniciada.</exception>
    public async Task<SesionAlmacenada> RequerirSesionAsync(CancellationToken cancelacion = default)
        => await CargarSesionAsync(cancelacion).ConfigureAwait(false)
           ?? throw new ExcepcionApi(
               HttpStatusCode.Unauthorized,
               "No hay ninguna sesión iniciada. Ejecute primero 'chat login'.");

    /// <summary>Cierra la sesión local borrando los tokens del disco.</summary>
    public void CerrarSesion()
    {
        _almacen.Borrar();
        _sesion = null;
    }

    /// <summary>Registra una cuenta nueva y guarda la sesión resultante.</summary>
    /// <param name="nombreUsuario">Nombre de usuario.</param>
    /// <param name="email">Correo electrónico.</param>
    /// <param name="clave">Contraseña.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    public Task<SesionAlmacenada> RegistrarAsync(
        string nombreUsuario,
        string email,
        string clave,
        CancellationToken cancelacion = default)
        => AutenticarAsync("/api/auth/registrar", new SolicitudRegistroDto(nombreUsuario, email, clave), cancelacion);

    /// <summary>Inicia sesión y guarda los tokens.</summary>
    /// <param name="nombreUsuario">Nombre de usuario.</param>
    /// <param name="clave">Contraseña.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    public Task<SesionAlmacenada> IniciarSesionAsync(
        string nombreUsuario,
        string clave,
        CancellationToken cancelacion = default)
        => AutenticarAsync("/api/auth/login", new SolicitudLoginDto(nombreUsuario, clave), cancelacion);

    /// <summary>
    /// Obtiene la configuración pública del servidor (ruta del hub, límites).
    /// Es un endpoint anónimo, por lo que no requiere sesión iniciada.
    /// </summary>
    /// <param name="cancelacion">Token de cancelación.</param>
    public async Task<ConfiguracionPlataformaDto> ObtenerConfiguracionAsync(CancellationToken cancelacion = default)
    {
        using var respuesta = await _http.GetAsync("/api/configuracion", cancelacion).ConfigureAwait(false);
        return await LeerRespuestaAsync<ConfiguracionPlataformaDto>(respuesta, cancelacion).ConfigureAwait(false);
    }

    /// <summary>Lista todas las salas de la plataforma.</summary>
    /// <param name="cancelacion">Token de cancelación.</param>
    public Task<IReadOnlyList<SalaDto>> ObtenerSalasAsync(CancellationToken cancelacion = default)
        => EnviarAsync<IReadOnlyList<SalaDto>>(HttpMethod.Get, "/api/salas", null, cancelacion);

    /// <summary>Lista las salas a las que pertenece el usuario autenticado.</summary>
    /// <param name="cancelacion">Token de cancelación.</param>
    public Task<IReadOnlyList<SalaDto>> ObtenerSalasPropiasAsync(CancellationToken cancelacion = default)
        => EnviarAsync<IReadOnlyList<SalaDto>>(HttpMethod.Get, "/api/salas/mias", null, cancelacion);

    /// <summary>Crea una sala nueva.</summary>
    /// <param name="nombre">Nombre de la sala.</param>
    /// <param name="descripcion">Descripción opcional.</param>
    /// <param name="privada">Crea la sala como privada: solo la verán quienes sean invitados.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    public Task<SalaDto> CrearSalaAsync(
        string nombre,
        string? descripcion,
        bool privada = false,
        CancellationToken cancelacion = default)
        => EnviarAsync<SalaDto>(
            HttpMethod.Post,
            "/api/salas",
            new SolicitudCrearSalaDto(nombre, descripcion, privada),
            cancelacion);

    /// <summary>Abre o recupera la conversación privada con otra persona.</summary>
    /// <param name="nombreUsuario">Nombre del interlocutor.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    public Task<SalaDto> AbrirConversacionDirectaAsync(
        string nombreUsuario,
        CancellationToken cancelacion = default)
        => EnviarAsync<SalaDto>(
            HttpMethod.Post,
            "/api/salas/directas",
            new SolicitudConversacionDirectaDto(nombreUsuario),
            cancelacion);

    /// <summary>Incorpora a otra persona a una sala.</summary>
    /// <param name="salaId">Sala destino.</param>
    /// <param name="nombreUsuario">Nombre del invitado.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    public Task<ResultadoOperacionDto> InvitarASalaAsync(
        Guid salaId,
        string nombreUsuario,
        CancellationToken cancelacion = default)
        => EnviarAsync<ResultadoOperacionDto>(
            HttpMethod.Post,
            $"/api/salas/{salaId}/invitar",
            new SolicitudInvitarDto(nombreUsuario),
            cancelacion);

    /// <summary>Lista los miembros de una sala con su estado de conexión.</summary>
    /// <param name="salaId">Sala consultada.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    public Task<IReadOnlyList<MiembroSalaDto>> ObtenerMiembrosAsync(
        Guid salaId,
        CancellationToken cancelacion = default)
        => EnviarAsync<IReadOnlyList<MiembroSalaDto>>(
            HttpMethod.Get,
            $"/api/salas/{salaId}/miembros",
            null,
            cancelacion);

    /// <summary>Pone a cero los mensajes pendientes del usuario en una sala.</summary>
    /// <param name="salaId">Sala leída.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    public Task<ResultadoOperacionDto> MarcarLeidaAsync(Guid salaId, CancellationToken cancelacion = default)
        => EnviarAsync<ResultadoOperacionDto>(HttpMethod.Post, $"/api/salas/{salaId}/leida", null, cancelacion);

    /// <summary>Obtiene el estado de conexión de los usuarios conocidos por el servidor.</summary>
    /// <param name="cancelacion">Token de cancelación.</param>
    public Task<IReadOnlyList<PresenciaDto>> ObtenerPresenciaAsync(CancellationToken cancelacion = default)
        => EnviarAsync<IReadOnlyList<PresenciaDto>>(HttpMethod.Get, "/api/usuarios/presencia", null, cancelacion);

    /// <summary>Une al usuario a una sala.</summary>
    /// <param name="salaId">Sala destino.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    public Task<SalaDto> UnirseSalaAsync(Guid salaId, CancellationToken cancelacion = default)
        => EnviarAsync<SalaDto>(HttpMethod.Post, $"/api/salas/{salaId}/unirse", null, cancelacion);

    /// <summary>Saca al usuario de una sala.</summary>
    /// <param name="salaId">Sala de origen.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    public Task<ResultadoOperacionDto> SalirSalaAsync(Guid salaId, CancellationToken cancelacion = default)
        => EnviarAsync<ResultadoOperacionDto>(HttpMethod.Post, $"/api/salas/{salaId}/salir", null, cancelacion);

    /// <summary>Lista los usuarios de la plataforma.</summary>
    /// <param name="cancelacion">Token de cancelación.</param>
    public Task<IReadOnlyList<UsuarioDto>> ObtenerUsuariosAsync(CancellationToken cancelacion = default)
        => EnviarAsync<IReadOnlyList<UsuarioDto>>(HttpMethod.Get, "/api/usuarios", null, cancelacion);

    /// <summary>Obtiene el historial reciente de una sala.</summary>
    /// <param name="salaId">Sala consultada.</param>
    /// <param name="cantidad">Número máximo de mensajes.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    public Task<IReadOnlyList<MensajeDto>> ObtenerMensajesAsync(
        Guid salaId,
        int cantidad,
        CancellationToken cancelacion = default)
        => EnviarAsync<IReadOnlyList<MensajeDto>>(
            HttpMethod.Get,
            $"/api/mensajes?salaId={salaId}&cantidad={cantidad}",
            null,
            cancelacion);

    /// <summary>Publica un mensaje sin usar el hub (útil para el comando «enviar»).</summary>
    /// <param name="salaId">Sala destino.</param>
    /// <param name="texto">Contenido en claro.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    public Task<MensajeDto> EnviarMensajeAsync(Guid salaId, string texto, CancellationToken cancelacion = default)
        => EnviarAsync<MensajeDto>(
            HttpMethod.Post,
            "/api/mensajes",
            new SolicitudEnviarMensajeDto(salaId, texto, Guid.CreateVersion7()),
            cancelacion);

    /// <summary>
    /// Busca una conversación por nombre (insensible a mayúsculas). Mira primero
    /// entre las propias —que es donde están las conversaciones directas, listadas
    /// con el nombre del interlocutor— y después en el catálogo público.
    /// </summary>
    /// <param name="nombre">Nombre buscado.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    /// <returns>La sala encontrada.</returns>
    /// <exception cref="ExcepcionApi">Si no existe ninguna sala con ese nombre.</exception>
    public async Task<SalaDto> ResolverSalaAsync(string nombre, CancellationToken cancelacion = default)
    {
        var propias = await ObtenerSalasPropiasAsync(cancelacion).ConfigureAwait(false);
        var propia = Buscar(propias, nombre);

        if (propia is not null)
        {
            return propia;
        }

        var catalogo = await ObtenerSalasAsync(cancelacion).ConfigureAwait(false);

        return Buscar(catalogo, nombre)
            ?? throw new ExcepcionApi(
                HttpStatusCode.NotFound,
                $"No existe ninguna sala llamada '{nombre}'. Use 'chat salas' para ver las disponibles.");
    }

    /// <summary>Busca una sala por nombre dentro de una lista ya descargada.</summary>
    /// <param name="salas">Lista donde buscar.</param>
    /// <param name="nombre">Nombre buscado.</param>
    private static SalaDto? Buscar(IReadOnlyList<SalaDto> salas, string nombre)
        => salas.FirstOrDefault(s => string.Equals(s.Nombre, nombre, StringComparison.OrdinalIgnoreCase));

    /// <summary>Devuelve un token de acceso vigente, renovándolo si hace falta.</summary>
    /// <param name="cancelacion">Token de cancelación.</param>
    public async Task<string> ObtenerTokenVigenteAsync(CancellationToken cancelacion = default)
    {
        var sesion = await RequerirSesionAsync(cancelacion).ConfigureAwait(false);

        if (sesion.AccesoVigente(DateTimeOffset.UtcNow))
        {
            return sesion.TokenAcceso;
        }

        var renovada = await RefrescarAsync(cancelacion).ConfigureAwait(false);
        return renovada.TokenAcceso;
    }

    /// <summary>Ejecuta una autenticación y persiste la sesión devuelta.</summary>
    private async Task<SesionAlmacenada> AutenticarAsync(
        string ruta,
        object cuerpo,
        CancellationToken cancelacion)
    {
        using var peticion = new HttpRequestMessage(HttpMethod.Post, ruta)
        {
            Content = JsonContent.Create(cuerpo, options: OpcionesJson)
        };

        using var respuesta = await _http.SendAsync(peticion, cancelacion).ConfigureAwait(false);
        var autenticacion = await LeerRespuestaAsync<RespuestaAutenticacionDto>(respuesta, cancelacion)
            .ConfigureAwait(false);

        var sesion = new SesionAlmacenada(
            autenticacion.UsuarioId,
            autenticacion.NombreUsuario,
            autenticacion.TokenAcceso,
            autenticacion.ExpiraEn,
            autenticacion.TokenRefresco,
            autenticacion.Roles,
            _opciones.UrlServidor);

        await _almacen.GuardarAsync(sesion, cancelacion).ConfigureAwait(false);
        _sesion = sesion;

        return sesion;
    }

    /// <summary>Renueva la sesión con el token de refresco, con exclusión mutua.</summary>
    private async Task<SesionAlmacenada> RefrescarAsync(CancellationToken cancelacion)
    {
        await _cerrojoRefresco.WaitAsync(cancelacion).ConfigureAwait(false);
        try
        {
            var sesion = await RequerirSesionAsync(cancelacion).ConfigureAwait(false);

            // Otra llamada pudo renovarla mientras se esperaba el cerrojo.
            if (sesion.AccesoVigente(DateTimeOffset.UtcNow))
            {
                return sesion;
            }

            return await AutenticarAsync(
                "/api/auth/refrescar",
                new SolicitudRefrescoDto(sesion.TokenRefresco),
                cancelacion).ConfigureAwait(false);
        }
        finally
        {
            _cerrojoRefresco.Release();
        }
    }

    /// <summary>
    /// Envía una petición autenticada, renovando la sesión y reintentando una vez
    /// si el servidor responde 401.
    /// </summary>
    private async Task<T> EnviarAsync<T>(
        HttpMethod metodo,
        string ruta,
        object? cuerpo,
        CancellationToken cancelacion)
    {
        var token = await ObtenerTokenVigenteAsync(cancelacion).ConfigureAwait(false);

        var respuesta = await EnviarConTokenAsync(metodo, ruta, cuerpo, token, cancelacion).ConfigureAwait(false);

        if (respuesta.StatusCode == HttpStatusCode.Unauthorized)
        {
            respuesta.Dispose();

            var renovada = await RefrescarAsync(cancelacion).ConfigureAwait(false);
            respuesta = await EnviarConTokenAsync(metodo, ruta, cuerpo, renovada.TokenAcceso, cancelacion)
                .ConfigureAwait(false);
        }

        using (respuesta)
        {
            return await LeerRespuestaAsync<T>(respuesta, cancelacion).ConfigureAwait(false);
        }
    }

    /// <summary>Construye y envía una petición con el token indicado.</summary>
    private async Task<HttpResponseMessage> EnviarConTokenAsync(
        HttpMethod metodo,
        string ruta,
        object? cuerpo,
        string token,
        CancellationToken cancelacion)
    {
        using var peticion = new HttpRequestMessage(metodo, ruta);
        peticion.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        if (cuerpo is not null)
        {
            peticion.Content = JsonContent.Create(cuerpo, cuerpo.GetType(), options: OpcionesJson);
        }

        return await _http.SendAsync(peticion, cancelacion).ConfigureAwait(false);
    }

    /// <summary>Deserializa una respuesta correcta o traduce el error del servidor.</summary>
    private static async Task<T> LeerRespuestaAsync<T>(HttpResponseMessage respuesta, CancellationToken cancelacion)
    {
        if (respuesta.IsSuccessStatusCode)
        {
            var valor = await respuesta.Content
                .ReadFromJsonAsync<T>(OpcionesJson, cancelacion)
                .ConfigureAwait(false);

            return valor ?? throw new ExcepcionApi(
                respuesta.StatusCode,
                "El servidor devolvió una respuesta vacía.");
        }

        throw await ConstruirErrorAsync(respuesta, cancelacion).ConfigureAwait(false);
    }

    /// <summary>Traduce un <c>ProblemDetails</c> a una excepción con mensaje legible.</summary>
    private static async Task<ExcepcionApi> ConstruirErrorAsync(
        HttpResponseMessage respuesta,
        CancellationToken cancelacion)
    {
        try
        {
            var problema = await respuesta.Content
                .ReadFromJsonAsync<ProblemaDto>(OpcionesJson, cancelacion)
                .ConfigureAwait(false);

            if (problema is not null && !string.IsNullOrWhiteSpace(problema.Detail))
            {
                return new ExcepcionApi(respuesta.StatusCode, problema.Detail, problema.Errores);
            }
        }
        catch (Exception excepcion) when (excepcion is JsonException or NotSupportedException)
        {
            // El servidor no devolvió JSON; se cae al mensaje genérico de abajo.
        }

        return new ExcepcionApi(
            respuesta.StatusCode,
            $"El servidor respondió {(int)respuesta.StatusCode} ({respuesta.ReasonPhrase}).");
    }

    /// <summary>Proyección del <c>ProblemDetails</c> devuelto por el servidor.</summary>
    /// <param name="Title">Título del problema.</param>
    /// <param name="Detail">Descripción legible.</param>
    /// <param name="Errores">Errores por campo, si los hay.</param>
    private sealed record ProblemaDto(
        string? Title,
        string? Detail,
        Dictionary<string, string[]>? Errores);
}
