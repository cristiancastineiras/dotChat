using System.Net.Http.Headers;
using Chat.Aplicacion.Abstracciones;
using Chat.Aplicacion.Cqrs;
using Chat.Aplicacion.Opciones;
using Chat.Dominio.Constantes;
using Chat.Infraestructura.Seguridad;
using Chat.Infraestructura.Tiempo;
using Chat.Servidor.Configuracion;
using Chat.Servidor.Endpoints;
using Chat.Tests.Comun;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Chat.Tests.Servidor;

/// <summary>
/// Servidor HTTP en memoria que monta la canalización real: autenticación JWT,
/// políticas de autorización, limitación de peticiones, manejador global de
/// excepciones y los mismos grupos de rutas que registra el servidor de verdad.
/// </summary>
/// <remarks>
/// <para>
/// Lo único que se sustituye es el despachador CQRS, cuyos manejadores ya tienen sus
/// propias pruebas. Así lo que se comprueba aquí es exactamente lo que aporta la capa
/// HTTP: qué ruta llama a qué operación, de dónde sale la identidad, qué código de
/// estado se devuelve y cómo se traduce un error.
/// </para>
/// <para>
/// No se arranca el <c>Program</c> completo a propósito: ese exige PostgreSQL, MinIO y
/// Valkey levantados, y una prueba que necesita medio despliegue para correr deja de
/// ejecutarse a los dos días.
/// </para>
/// </remarks>
public sealed class ServidorDePrueba : IAsyncDisposable
{
    private readonly WebApplication _aplicacion;

    /// <summary>Monta y arranca el servidor.</summary>
    public ServidorDePrueba()
    {
        var constructor = WebApplication.CreateBuilder();

        constructor.Logging.ClearProviders();
        constructor.WebHost.UseTestServer();

        constructor.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Emisor"] = "dotchat-pruebas",
            ["Jwt:Audiencia"] = "dotchat-clientes",
            ["Jwt:ClaveFirmaBase64"] = Opciones.ClaveFirmaBase64,
            ["Jwt:MinutosVigenciaAcceso"] = "30"
        });

        constructor.Services.AddSingleton(Despachador);
        constructor.Services.AddSingleton(Configuracion);
        constructor.Services.AddSingleton<IProveedorFechaHora>(Reloj);
        constructor.Services.Configure<AdjuntosOptions>(_ => { });

        constructor.Services.AgregarAutenticacionJwt(constructor.Configuration);
        constructor.Services.AgregarLimitacionPeticiones();
        constructor.Services.AddProblemDetails();
        constructor.Services.AddExceptionHandler<ManejadorExcepcionesGlobal>();

        _aplicacion = constructor.Build();

        _aplicacion.UseExceptionHandler();
        _aplicacion.UseRateLimiter();
        _aplicacion.UseAuthentication();
        _aplicacion.UseAuthorization();

        _aplicacion.MapearEndpointsDiagnostico();
        _aplicacion.MapearEndpointsAutenticacion();
        _aplicacion.MapearEndpointsUsuarios();
        _aplicacion.MapearEndpointsSalas();
        _aplicacion.MapearEndpointsMensajes();
        _aplicacion.MapearEndpointsAdjuntos();
        _aplicacion.MapearEndpointsAdministracion();

        _aplicacion.StartAsync().GetAwaiter().GetResult();
    }

    /// <summary>Despachador sustituido; es donde la prueba programa el resultado esperado.</summary>
    public IDespachador Despachador { get; } = Substitute.For<IDespachador>();

    /// <summary>Servicio de configuración pública sustituido.</summary>
    public IServicioConfiguracionPlataforma Configuracion { get; } =
        Substitute.For<IServicioConfiguracionPlataforma>();

    /// <summary>Reloj detenido del servidor.</summary>
    public RelojFijo Reloj { get; } = new();

    /// <summary>Identificador del usuario con el que se autentican las peticiones.</summary>
    public Guid UsuarioId { get; } = Guid.CreateVersion7();

    /// <summary>Cliente sin autenticar.</summary>
    /// <returns>Cliente HTTP contra el servidor en memoria.</returns>
    public HttpClient Anonimo() => _aplicacion.GetTestClient();

    /// <summary>Cliente autenticado como usuario corriente.</summary>
    /// <param name="usuarioId">Identidad a usar; por defecto, la del servidor.</param>
    public HttpClient ComoUsuario(Guid? usuarioId = null)
        => Autenticado(usuarioId ?? UsuarioId, "ana", RolesDelSistema.Usuario);

    /// <summary>Cliente autenticado como administrador.</summary>
    public HttpClient ComoAdministrador()
        => Autenticado(UsuarioId, "admin", RolesDelSistema.Administrador, RolesDelSistema.Usuario);

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _aplicacion.StopAsync().ConfigureAwait(false);
        await _aplicacion.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Construye un cliente que presenta un token firmado de verdad, para que la
    /// prueba atraviese la validación real del esquema JwtBearer.
    /// </summary>
    /// <param name="usuarioId">Sujeto del token.</param>
    /// <param name="nombre">Nombre de usuario.</param>
    /// <param name="roles">Roles que se incluyen como claims.</param>
    private HttpClient Autenticado(Guid usuarioId, string nombre, params string[] roles)
    {
        // El token se emite con la hora real y no con el reloj detenido del servidor:
        // la validación de vigencia la hace el esquema JwtBearer contra el reloj del
        // sistema, y un token fechado en otro momento se rechazaría por caducado.
        var generador = new GeneradorTokensJwt(
            Comun.Opciones.De(Comun.Opciones.Jwt()),
            new ProveedorFechaHoraSistema());

        var usuario = Datos.Usuario(usuarioId, nombre);

        var cliente = _aplicacion.GetTestClient();
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            generador.GenerarTokenAcceso(usuario, roles).Valor);

        return cliente;
    }
}
