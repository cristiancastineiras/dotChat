using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Chat.Aplicacion.Dtos;
using Chat.Dominio.Constantes;
using Microsoft.Extensions.Options;
using Spectre.Console;

namespace Chat.AdminCli.Servicios;

/// <summary>
/// Cliente HTTP de los endpoints administrativos. Se autentica con las credenciales
/// configuradas y, si faltan, las pide por consola una sola vez. Los tokens viven en
/// memoria mientras la consola siga abierta.
/// </summary>
/// <remarks>
/// La petición de credenciales está deliberadamente separada de las llamadas a la API:
/// Spectre.Console no admite dos funciones interactivas a la vez, y pedir una contraseña
/// desde dentro de un indicador de progreso aborta la orden con el error «Trying to run
/// one or more interactive functions concurrently». Por eso <see cref="PrepararSesionAsync"/>
/// —la única que puede dibujar en pantalla— se invoca antes de arrancar ningún spinner, y
/// las renovaciones posteriores usan el token de refresco, que nunca necesita teclado.
/// </remarks>
public sealed class ClienteAdminApi
{
    private static readonly JsonSerializerOptions OpcionesJson = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly OpcionesAdmin _opciones;
    private readonly SemaphoreSlim _cerrojoSesion = new(1, 1);
    private RespuestaAutenticacionDto? _sesion;

    /// <summary>Crea el cliente.</summary>
    /// <param name="http">Cliente HTTP configurado con la dirección base.</param>
    /// <param name="opciones">Configuración de la consola de administración.</param>
    public ClienteAdminApi(HttpClient http, IOptions<OpcionesAdmin> opciones)
    {
        _http = http;
        _opciones = opciones.Value;
    }

    /// <summary>Dirección base del servidor.</summary>
    public string UrlServidor => _opciones.UrlServidor;

    /// <summary>Nombre del administrador autenticado, si ya se ha iniciado sesión.</summary>
    public string? NombreUsuario => _sesion?.NombreUsuario;

    /// <summary>
    /// Garantiza que hay una sesión de administrador utilizable, pidiendo las
    /// credenciales por consola si hiciera falta.
    /// </summary>
    /// <param name="cancelacion">Token de cancelación.</param>
    /// <remarks>
    /// Debe llamarse siempre fuera de un indicador de progreso o de cualquier otra
    /// pantalla activa de Spectre.Console, porque puede abrir un cuadro de entrada.
    /// </remarks>
    public async Task PrepararSesionAsync(CancellationToken cancelacion = default)
    {
        if (SesionVigente)
        {
            return;
        }

        // Si ya hay un refresco válido, se renueva sin molestar al usuario.
        if (_sesion is not null && await IntentarRefrescarAsync(cancelacion).ConfigureAwait(false))
        {
            return;
        }

        var usuario = string.IsNullOrWhiteSpace(_opciones.NombreUsuario)
            ? AnsiConsole.Prompt(new TextPrompt<string>("[bold]Administrador:[/]").PromptStyle("orange1"))
            : _opciones.NombreUsuario;

        var clave = string.IsNullOrWhiteSpace(_opciones.Clave)
            ? AnsiConsole.Prompt(new TextPrompt<string>("[bold]Contraseña:[/]").PromptStyle("orange1").Secret())
            : _opciones.Clave;

        await IniciarSesionAsync(usuario, clave, cancelacion).ConfigureAwait(false);
    }

    /// <summary>Lista los usuarios, incluidas las cuentas desactivadas.</summary>
    /// <param name="cancelacion">Token de cancelación.</param>
    public Task<IReadOnlyList<UsuarioDto>> ListarUsuariosAsync(CancellationToken cancelacion = default)
        => EnviarAsync<IReadOnlyList<UsuarioDto>>(
            HttpMethod.Get,
            "/api/usuarios?incluirInactivos=true",
            null,
            cancelacion);

    /// <summary>Obtiene el estado de conexión de los usuarios conocidos por el servidor.</summary>
    /// <param name="cancelacion">Token de cancelación.</param>
    public Task<IReadOnlyList<PresenciaDto>> ObtenerPresenciaAsync(CancellationToken cancelacion = default)
        => EnviarAsync<IReadOnlyList<PresenciaDto>>(HttpMethod.Get, "/api/usuarios/presencia", null, cancelacion);

    /// <summary>Elimina definitivamente una cuenta.</summary>
    /// <param name="usuarioId">Usuario a eliminar.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    public Task<ResultadoOperacionDto> EliminarUsuarioAsync(Guid usuarioId, CancellationToken cancelacion = default)
        => EnviarAsync<ResultadoOperacionDto>(
            HttpMethod.Delete,
            $"/api/admin/usuarios/{usuarioId}",
            null,
            cancelacion);

    /// <summary>Lista todas las salas, incluidas las privadas y las conversaciones directas.</summary>
    /// <param name="cancelacion">Token de cancelación.</param>
    public Task<IReadOnlyList<SalaDto>> ListarSalasAsync(CancellationToken cancelacion = default)
        => EnviarAsync<IReadOnlyList<SalaDto>>(HttpMethod.Get, "/api/admin/salas", null, cancelacion);

    /// <summary>Crea una sala nueva.</summary>
    /// <param name="nombre">Nombre de la sala.</param>
    /// <param name="descripcion">Descripción opcional.</param>
    /// <param name="privada">Crea la sala como privada.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    public Task<SalaDto> CrearSalaAsync(
        string nombre,
        string? descripcion,
        bool privada,
        CancellationToken cancelacion = default)
        => EnviarAsync<SalaDto>(
            HttpMethod.Post,
            "/api/salas",
            new SolicitudCrearSalaDto(nombre, descripcion, privada),
            cancelacion);

    /// <summary>Elimina una sala y su historial.</summary>
    /// <param name="salaId">Sala a eliminar.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    public Task<ResultadoOperacionDto> EliminarSalaAsync(Guid salaId, CancellationToken cancelacion = default)
        => EnviarAsync<ResultadoOperacionDto>(HttpMethod.Delete, $"/api/admin/salas/{salaId}", null, cancelacion);

    /// <summary>Lista los miembros de una sala con su estado de conexión.</summary>
    /// <param name="salaId">Sala consultada.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    public Task<IReadOnlyList<MiembroSalaDto>> ListarMiembrosAsync(
        Guid salaId,
        CancellationToken cancelacion = default)
        => EnviarAsync<IReadOnlyList<MiembroSalaDto>>(
            HttpMethod.Get,
            $"/api/salas/{salaId}/miembros",
            null,
            cancelacion);

    /// <summary>Obtiene el historial de una sala (un administrador puede auditar cualquiera).</summary>
    /// <param name="salaId">Sala consultada.</param>
    /// <param name="cantidad">Número máximo de mensajes.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    public Task<IReadOnlyList<MensajeDto>> ListarMensajesAsync(
        Guid salaId,
        int cantidad,
        CancellationToken cancelacion = default)
        => EnviarAsync<IReadOnlyList<MensajeDto>>(
            HttpMethod.Get,
            $"/api/mensajes?salaId={salaId}&cantidad={cantidad}",
            null,
            cancelacion);

    /// <summary>Vacía la caché del servidor.</summary>
    /// <param name="cancelacion">Token de cancelación.</param>
    public Task<ResultadoOperacionDto> LimpiarCacheAsync(CancellationToken cancelacion = default)
        => EnviarAsync<ResultadoOperacionDto>(HttpMethod.Post, "/api/admin/cache/limpiar", null, cancelacion);

    /// <summary>Obtiene el resumen de actividad de la plataforma.</summary>
    /// <param name="cancelacion">Token de cancelación.</param>
    public Task<EstadisticasDto> ObtenerEstadisticasAsync(CancellationToken cancelacion = default)
        => EnviarAsync<EstadisticasDto>(HttpMethod.Get, "/api/admin/estadisticas", null, cancelacion);

    /// <summary>Lista las conexiones SignalR activas.</summary>
    /// <param name="cancelacion">Token de cancelación.</param>
    public Task<IReadOnlyList<ConexionActivaDto>> ObtenerConexionesAsync(CancellationToken cancelacion = default)
        => EnviarAsync<IReadOnlyList<ConexionActivaDto>>(HttpMethod.Get, "/api/admin/conexiones", null, cancelacion);

    /// <summary>Busca una sala por nombre entre las existentes.</summary>
    /// <param name="nombre">Nombre buscado.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    /// <exception cref="ExcepcionAdminApi">Si no existe ninguna sala con ese nombre.</exception>
    public async Task<SalaDto> ResolverSalaAsync(string nombre, CancellationToken cancelacion = default)
    {
        var salas = await ListarSalasAsync(cancelacion).ConfigureAwait(false);

        return salas.FirstOrDefault(s => string.Equals(s.Nombre, nombre, StringComparison.OrdinalIgnoreCase))
            ?? throw new ExcepcionAdminApi(
                HttpStatusCode.NotFound,
                $"No existe ninguna sala llamada '{nombre}'.");
    }

    /// <summary>Busca un usuario por nombre entre los existentes.</summary>
    /// <param name="nombre">Nombre buscado.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    /// <exception cref="ExcepcionAdminApi">Si no existe ningún usuario con ese nombre.</exception>
    public async Task<UsuarioDto> ResolverUsuarioAsync(string nombre, CancellationToken cancelacion = default)
    {
        var usuarios = await ListarUsuariosAsync(cancelacion).ConfigureAwait(false);

        return usuarios.FirstOrDefault(u => string.Equals(u.NombreUsuario, nombre, StringComparison.OrdinalIgnoreCase))
            ?? throw new ExcepcionAdminApi(
                HttpStatusCode.NotFound,
                $"No existe ningún usuario llamado '{nombre}'.");
    }

    /// <summary>Indica si el token de acceso actual sigue siendo utilizable.</summary>
    private bool SesionVigente => _sesion is not null && _sesion.ExpiraEn > DateTimeOffset.UtcNow.AddSeconds(30);

    /// <summary>Inicia sesión con credenciales y comprueba que la cuenta sea administradora.</summary>
    /// <param name="usuario">Nombre del administrador.</param>
    /// <param name="clave">Contraseña.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    private async Task IniciarSesionAsync(string usuario, string clave, CancellationToken cancelacion)
    {
        using var peticion = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(new SolicitudLoginDto(usuario, clave), options: OpcionesJson)
        };

        using var respuesta = await _http.SendAsync(peticion, cancelacion).ConfigureAwait(false);
        var sesion = await LeerRespuestaAsync<RespuestaAutenticacionDto>(respuesta, cancelacion).ConfigureAwait(false);

        if (!sesion.Roles.Contains(RolesDelSistema.Administrador))
        {
            throw new ExcepcionAdminApi(
                HttpStatusCode.Forbidden,
                $"La cuenta '{sesion.NombreUsuario}' no tiene el rol '{RolesDelSistema.Administrador}'.");
        }

        _sesion = sesion;
    }

    /// <summary>
    /// Renueva la sesión con el token de refresco. No pide nada por teclado, de modo
    /// que puede ejecutarse con un indicador de progreso en pantalla.
    /// </summary>
    /// <param name="cancelacion">Token de cancelación.</param>
    /// <returns><c>true</c> si la sesión quedó renovada.</returns>
    private async Task<bool> IntentarRefrescarAsync(CancellationToken cancelacion)
    {
        var anterior = _sesion;

        if (anterior is null)
        {
            return false;
        }

        await _cerrojoSesion.WaitAsync(cancelacion).ConfigureAwait(false);
        try
        {
            // Otra llamada pudo renovarla mientras se esperaba el cerrojo.
            if (SesionVigente)
            {
                return true;
            }

            using var peticion = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refrescar")
            {
                Content = JsonContent.Create(
                    new SolicitudRefrescoDto(anterior.TokenRefresco),
                    options: OpcionesJson)
            };

            using var respuesta = await _http.SendAsync(peticion, cancelacion).ConfigureAwait(false);

            if (!respuesta.IsSuccessStatusCode)
            {
                // El refresco caducó o ya se usó: habrá que volver a pedir credenciales
                // en el próximo hueco no interactivo.
                _sesion = null;
                return false;
            }

            _sesion = await LeerRespuestaAsync<RespuestaAutenticacionDto>(respuesta, cancelacion)
                .ConfigureAwait(false);

            return true;
        }
        finally
        {
            _cerrojoSesion.Release();
        }
    }

    /// <summary>Envía una petición administrativa autenticada.</summary>
    private async Task<T> EnviarAsync<T>(
        HttpMethod metodo,
        string ruta,
        object? cuerpo,
        CancellationToken cancelacion)
    {
        if (!SesionVigente && !await IntentarRefrescarAsync(cancelacion).ConfigureAwait(false))
        {
            throw new ExcepcionAdminApi(
                HttpStatusCode.Unauthorized,
                "La sesión de administrador ha caducado. Vuelva a ejecutar la orden para autenticarse de nuevo.");
        }

        using var peticion = new HttpRequestMessage(metodo, ruta);
        peticion.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _sesion!.TokenAcceso);

        if (cuerpo is not null)
        {
            peticion.Content = JsonContent.Create(cuerpo, cuerpo.GetType(), options: OpcionesJson);
        }

        using var respuesta = await _http.SendAsync(peticion, cancelacion).ConfigureAwait(false);
        return await LeerRespuestaAsync<T>(respuesta, cancelacion).ConfigureAwait(false);
    }

    /// <summary>Deserializa una respuesta correcta o traduce el error del servidor.</summary>
    private static async Task<T> LeerRespuestaAsync<T>(HttpResponseMessage respuesta, CancellationToken cancelacion)
    {
        if (respuesta.IsSuccessStatusCode)
        {
            var valor = await respuesta.Content
                .ReadFromJsonAsync<T>(OpcionesJson, cancelacion)
                .ConfigureAwait(false);

            return valor ?? throw new ExcepcionAdminApi(
                respuesta.StatusCode,
                "El servidor devolvió una respuesta vacía.");
        }

        string? detalle = null;
        try
        {
            var problema = await respuesta.Content
                .ReadFromJsonAsync<ProblemaDto>(OpcionesJson, cancelacion)
                .ConfigureAwait(false);

            detalle = problema?.Detail;
        }
        catch (Exception excepcion) when (excepcion is JsonException or NotSupportedException)
        {
            // El servidor no devolvió JSON: se usa el mensaje genérico.
        }

        throw new ExcepcionAdminApi(
            respuesta.StatusCode,
            detalle ?? $"El servidor respondió {(int)respuesta.StatusCode} ({respuesta.ReasonPhrase}).");
    }

    /// <summary>Proyección del <c>ProblemDetails</c> devuelto por el servidor.</summary>
    /// <param name="Title">Título del problema.</param>
    /// <param name="Detail">Descripción legible.</param>
    private sealed record ProblemaDto(string? Title, string? Detail);
}

/// <summary>Error devuelto por la API administrativa, ya traducido a un mensaje legible.</summary>
public sealed class ExcepcionAdminApi : Exception
{
    /// <summary>Código de estado HTTP recibido.</summary>
    public HttpStatusCode Estado { get; }

    /// <summary>Crea la excepción.</summary>
    /// <param name="estado">Código de estado HTTP.</param>
    /// <param name="mensaje">Mensaje legible.</param>
    public ExcepcionAdminApi(HttpStatusCode estado, string mensaje) : base(mensaje) => Estado = estado;
}
