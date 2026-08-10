using Chat.Aplicacion;
using Chat.Aplicacion.Abstracciones;
using Chat.Aplicacion.Opciones;
using Chat.Infraestructura;
using Chat.Infraestructura.Persistencia;
using Chat.Infraestructura.Seguridad;
using Chat.Servidor.Configuracion;
using Chat.Servidor.Endpoints;
using Chat.Servidor.Hubs;
using Chat.Servidor.Servicios;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;
using ZLogger;

var constructor = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Configuración
// ---------------------------------------------------------------------------
// Orden de precedencia: appsettings.json < appsettings.{Entorno}.json <
// user secrets (solo en desarrollo) < variables de entorno < argumentos.
// Las claves de firma y de cifrado llegan siempre por los dos últimos canales.
constructor.Configuration.AddEnvironmentVariables("DOTCHAT_");

// ---------------------------------------------------------------------------
// Registro estructurado
// ---------------------------------------------------------------------------
// Se usa ZLogger (Cysharp) en lugar del proveedor de consola estándar: escribe en
// UTF-8 sin construir cadenas intermedias y vuelca de forma asíncrona, de modo que
// registrar cada mensaje enviado no genera presión sobre el recolector de basura.
constructor.Logging.ClearProviders();
constructor.Logging.AddZLoggerConsole(opciones =>
{
    opciones.UsePlainTextFormatter(formateador =>
        formateador.SetPrefixFormatter(
            $"{0:yyyy-MM-dd HH:mm:ss} [{1:short}] ",
            (in MessageTemplate plantilla, in LogInfo informacion) =>
                plantilla.Format(informacion.Timestamp, informacion.LogLevel)));
});
constructor.Logging.AddDebug();

// ---------------------------------------------------------------------------
// Servicios
// ---------------------------------------------------------------------------
constructor.Services.AgregarOpciones(constructor.Configuration);
constructor.AgregarTelemetria();
constructor.Services.AgregarInfraestructura(constructor.Configuration);
constructor.Services.AgregarAplicacion();
constructor.Services.AgregarAutenticacionJwt(constructor.Configuration);
constructor.Services.AgregarLimitacionPeticiones();

constructor.Services.AddSingleton<IRegistroConexiones, RegistroConexiones>();
constructor.Services.AddSingleton<LimitadorEnvioMensajes>();
constructor.Services.AddScoped<INotificadorTiempoReal, NotificadorSignalR>();
constructor.Services.AddHostedService<ServicioMantenimiento>();

constructor.Services.AddProblemDetails();
constructor.Services.AddExceptionHandler<ManejadorExcepcionesGlobal>();

var opcionesSignalR = constructor.Configuration
    .GetSection(SignalROptions.Seccion)
    .Get<SignalROptions>() ?? new SignalROptions();

constructor.Services.AddSignalR(opciones =>
{
    opciones.EnableDetailedErrors = opcionesSignalR.DetallarErrores;
    opciones.ClientTimeoutInterval = TimeSpan.FromSeconds(opcionesSignalR.SegundosTiempoEsperaCliente);
    opciones.KeepAliveInterval = TimeSpan.FromSeconds(opcionesSignalR.SegundosIntervaloLatido);
    opciones.HandshakeTimeout = TimeSpan.FromSeconds(opcionesSignalR.SegundosTiempoNegociacion);
    opciones.MaximumReceiveMessageSize = opcionesSignalR.TamanoMaximoMensajeBytes;
});

// Solo se confía en las cabeceras de proxy si el despliegue las coloca delante.
constructor.Services.Configure<ForwardedHeadersOptions>(opciones =>
    opciones.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto);

var aplicacion = constructor.Build();

// ---------------------------------------------------------------------------
// Comprobaciones de arranque: mejor no arrancar que arrancar mal configurado
// ---------------------------------------------------------------------------
var registro = aplicacion.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Chat.Servidor.Arranque");

try
{
    var jwt = aplicacion.Services.GetRequiredService<IOptions<JwtOptions>>().Value;
    var cifrado = aplicacion.Services.GetRequiredService<IOptions<CifradoOptions>>().Value;
    var telemetria = aplicacion.Services.GetRequiredService<IOptions<TelemetriaOptions>>().Value;

    ExtensionesAutenticacion.ValidarClaveFirma(jwt.ClaveFirmaBase64);

    registro.LogInformation(
        "Configuración validada. Emisor={Emisor} Audiencia={Audiencia} HuellaClaveCifrado={Huella}",
        jwt.Emisor,
        jwt.Audiencia,
        ServicioCifradorMensajes.CalcularHuellaClave(cifrado.ClaveBase64));

    // Deja constancia de a dónde va la telemetría: si el receptor no está levantado,
    // el exportador falla en silencio y esta línea es la única pista.
    if (telemetria.Activada)
    {
        registro.LogInformation(
            "Telemetría activada. Destino={Destino} Protocolo={Protocolo} Trazas={Trazas} Métricas={Metricas} Registros={Registros}",
            telemetria.ResolverPuntoEntrada(),
            telemetria.Protocolo,
            telemetria.Trazas,
            telemetria.Metricas,
            telemetria.Registros);
    }
    else
    {
        registro.LogInformation("Telemetría desactivada por configuración.");
    }
}
catch (Exception excepcion)
{
    registro.LogCritical(
        excepcion,
        "No se puede arrancar el servidor: la configuración de seguridad es incorrecta. " +
        "Ejecute 'scripts/configurar-secretos.ps1' para generar las claves.");
    throw;
}

// ---------------------------------------------------------------------------
// Inicialización de la base de datos
// ---------------------------------------------------------------------------
await using (var ambito = aplicacion.Services.CreateAsyncScope())
{
    var inicializador = ambito.ServiceProvider.GetRequiredService<InicializadorBaseDatos>();
    await inicializador.InicializarAsync().ConfigureAwait(false);
}

// ---------------------------------------------------------------------------
// Canalización HTTP
// ---------------------------------------------------------------------------
aplicacion.UseForwardedHeaders();
aplicacion.UseExceptionHandler();
aplicacion.UseStatusCodePages();

if (!aplicacion.Environment.IsDevelopment())
{
    // HSTS solo fuera de desarrollo: en local se usan certificados de confianza propios.
    aplicacion.UseHsts();
}

aplicacion.UseHttpsRedirection();

// Cabeceras de endurecimiento aplicadas a todas las respuestas.
aplicacion.Use(async (contexto, siguiente) =>
{
    var cabeceras = contexto.Response.Headers;
    cabeceras["X-Content-Type-Options"] = "nosniff";
    cabeceras["X-Frame-Options"] = "DENY";
    cabeceras["Referrer-Policy"] = "no-referrer";
    cabeceras["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'";
    cabeceras["Cross-Origin-Resource-Policy"] = "same-origin";

    await siguiente(contexto).ConfigureAwait(false);
});

aplicacion.UseRateLimiter();
aplicacion.UseAuthentication();
aplicacion.UseAuthorization();

// ---------------------------------------------------------------------------
// Rutas
// ---------------------------------------------------------------------------
aplicacion.MapearEndpointsDiagnostico();
aplicacion.MapearEndpointsAutenticacion();
aplicacion.MapearEndpointsUsuarios();
aplicacion.MapearEndpointsSalas();
aplicacion.MapearEndpointsMensajes();
aplicacion.MapearEndpointsAdministracion();

aplicacion.MapHub<ChatHub>(opcionesSignalR.RutaHub);

registro.LogInformation("Servidor de dotChat iniciado. Hub={RutaHub}", opcionesSignalR.RutaHub);

await aplicacion.RunAsync().ConfigureAwait(false);

/// <summary>
/// Punto de entrada del servidor. Se declara explícitamente para que el proyecto
/// de pruebas pueda referenciarlo con <c>WebApplicationFactory</c>.
/// </summary>
public partial class Program;
