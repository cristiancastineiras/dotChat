using Chat.Aplicacion;
using Chat.Aplicacion.Abstracciones;
using Chat.Aplicacion.Opciones;
using Chat.Infraestructura;
using Chat.Infraestructura.Persistencia;
using Chat.Infraestructura.Presencia;
using Chat.Infraestructura.Seguridad;
using Chat.Servidor.Configuracion;
using Chat.Servidor.Endpoints;
using Chat.Servidor.Hubs;
using Chat.Servidor.Servicios;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Options;
using ZLogger;

// ---------------------------------------------------------------------------
// Modo sonda
// ---------------------------------------------------------------------------
// El mismo ejecutable sirve de sonda de salud para Docker. Es preferible a meter
// curl en la imagen de ejecución: no añade paquetes, no amplía la superficie del
// contenedor y la sonda siempre está en la misma versión que el servidor.
if (args.Contains("--comprobar-salud", StringComparer.Ordinal))
{
    return await ComprobarSaludAsync().ConfigureAwait(false);
}

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

constructor.Services.AddScoped<INotificadorTiempoReal, NotificadorSignalR>();
constructor.Services.AddHostedService<ServicioMantenimiento>();
constructor.Services.AddHostedService<ServicioLatidoReplica>();

constructor.Services.AddProblemDetails();
constructor.Services.AddExceptionHandler<ManejadorExcepcionesGlobal>();
constructor.Services.AgregarSalud(
    constructor.Configuration.GetSection(ValkeyOptions.Seccion).GetValue("Activado", true));

// ---------------------------------------------------------------------------
// Piezas del escalado horizontal
// ---------------------------------------------------------------------------
var opcionesValkey = constructor.Configuration
    .GetSection(ValkeyOptions.Seccion)
    .Get<ValkeyOptions>() ?? new ValkeyOptions();

var opcionesSignalR = constructor.Configuration
    .GetSection(SignalROptions.Seccion)
    .Get<SignalROptions>() ?? new SignalROptions();

var signalR = constructor.Services.AddSignalR(opciones =>
{
    opciones.EnableDetailedErrors = opcionesSignalR.DetallarErrores;
    opciones.ClientTimeoutInterval = TimeSpan.FromSeconds(opcionesSignalR.SegundosTiempoEsperaCliente);
    opciones.KeepAliveInterval = TimeSpan.FromSeconds(opcionesSignalR.SegundosIntervaloLatido);
    opciones.HandshakeTimeout = TimeSpan.FromSeconds(opcionesSignalR.SegundosTiempoNegociacion);
    opciones.MaximumReceiveMessageSize = opcionesSignalR.TamanoMaximoMensajeBytes;
});

if (opcionesValkey.Activado)
{
    // Canal entre réplicas. Sin él, un mensaje publicado en una instancia solo llegaría
    // a los miembros de la sala conectados a esa misma instancia: los demás no verían
    // nada hasta recargar el historial.
    signalR.AddStackExchangeRedis(opciones =>
    {
        opciones.Configuration = ExtensionesValkey.ConstruirConfiguracion(opcionesValkey);
        opciones.Configuration.ChannelPrefix =
            StackExchange.Redis.RedisChannel.Literal($"{opcionesValkey.Prefijo}:signalr");
    });

    // Anillo de claves compartido. Cada réplica genera el suyo al arrancar si no hay
    // uno común, y entonces lo que cifra una no lo puede descifrar otra.
    constructor.Services.AddDataProtection()
        .SetApplicationName("dotchat")
        .PersistKeysToStackExchangeRedis(
            StackExchange.Redis.ConnectionMultiplexer.Connect(
                ExtensionesValkey.ConstruirConfiguracion(opcionesValkey)),
            $"{opcionesValkey.Prefijo}:claves");
}

// El tamaño máximo de una subida se decide en un único sitio y se propaga al
// servidor: así Kestrel corta la transferencia en cuanto se pasa, sin llegar a
// almacenar un fichero que la aplicación va a rechazar de todos modos.
var opcionesAdjuntos = constructor.Configuration
    .GetSection(AdjuntosOptions.Seccion)
    .Get<AdjuntosOptions>() ?? new AdjuntosOptions();

// Se deja algo de holgura sobre el límite: el envío multiparte añade cabeceras,
// separadores y el resto de campos del formulario.
var margenMultiparte = opcionesAdjuntos.TamanoMaximoBytes + (64 * 1024);

constructor.Services.Configure<FormOptions>(opciones =>
{
    opciones.MultipartBodyLengthLimit = margenMultiparte;
    opciones.ValueLengthLimit = 8 * 1024;
    opciones.MultipartHeadersLengthLimit = 8 * 1024;
});

constructor.Services.Configure<KestrelServerOptions>(opciones =>
    opciones.Limits.MaxRequestBodySize = margenMultiparte);

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
    var identidadReplica = aplicacion.Services.GetRequiredService<IdentidadReplica>();

    ExtensionesAutenticacion.ValidarClaveFirma(jwt.ClaveFirmaBase64);

    registro.LogInformation(
        "Configuración validada. Replica={Replica} Emisor={Emisor} Audiencia={Audiencia} HuellaClaveCifrado={Huella}",
        identidadReplica.Nombre,
        jwt.Emisor,
        jwt.Audiencia,
        ServicioCifradorMensajes.CalcularHuellaClave(cifrado.ClaveBase64));

    registro.LogInformation(
        "Modo de ejecución. Valkey={Valkey} Escalado={Escalado}",
        opcionesValkey.Activado ? opcionesValkey.Conexion : "desactivado",
        opcionesValkey.Activado ? "clúster" : "instancia única");

    // Deja constancia de a dónde va cada señal: si un receptor no está levantado, el
    // exportador falla en silencio y estas líneas son la única pista.
    if (telemetria.Activada)
    {
        registro.LogInformation(
            "Telemetría activada. Trazas={Trazas} Métricas={Metricas} Registros={Registros}",
            telemetria.Trazas.Activado ? telemetria.Trazas.PuntoEntrada : "desactivadas",
            telemetria.Metricas.Activado ? telemetria.Metricas.PuntoEntrada : "desactivadas",
            telemetria.Registros.Activado ? telemetria.Registros.PuntoEntrada : "desactivados");
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
// Preparación de las dependencias externas
// ---------------------------------------------------------------------------
// Con varias réplicas, todas ejecutan esto a la vez. Las dos operaciones son
// idempotentes y EF Core toma un cerrojo en la base de datos mientras migra, así
// que la primera en llegar migra y las demás esperan y siguen.
await using (var ambito = aplicacion.Services.CreateAsyncScope())
{
    var inicializador = ambito.ServiceProvider.GetRequiredService<InicializadorBaseDatos>();
    await inicializador.InicializarAsync().ConfigureAwait(false);

    var almacen = ambito.ServiceProvider.GetRequiredService<IAlmacenObjetos>();
    await almacen.PrepararAsync().ConfigureAwait(false);
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

// Detrás de un balanceador, quien termina TLS es él y aquí solo llega tráfico en
// claro por la red interna: redirigir desde aquí provocaría un bucle de redirecciones.
if (constructor.Configuration.GetValue("Servidor:RedirigirHttps", defaultValue: true))
{
    aplicacion.UseHttpsRedirection();
}

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
aplicacion.MapearSalud();
aplicacion.MapearEndpointsDiagnostico();
aplicacion.MapearEndpointsAutenticacion();
aplicacion.MapearEndpointsUsuarios();
aplicacion.MapearEndpointsSalas();
aplicacion.MapearEndpointsMensajes();
aplicacion.MapearEndpointsAdjuntos();
aplicacion.MapearEndpointsAdministracion();

aplicacion.MapHub<ChatHub>(opcionesSignalR.RutaHub);

registro.LogInformation("Servidor de dotChat iniciado. Hub={RutaHub}", opcionesSignalR.RutaHub);

await aplicacion.RunAsync().ConfigureAwait(false);

return 0;

/// <summary>
/// Pregunta a la instancia local si está viva. Devuelve 0 si contesta y 1 si no,
/// que es lo que espera la instrucción <c>HEALTHCHECK</c> de Docker.
/// </summary>
static async Task<int> ComprobarSaludAsync()
{
    var puerto = Environment.GetEnvironmentVariable("ASPNETCORE_HTTP_PORTS") ?? "8080";

    using var cliente = new HttpClient { Timeout = TimeSpan.FromSeconds(4) };

    try
    {
        using var respuesta = await cliente
            .GetAsync(new Uri($"http://127.0.0.1:{puerto}/salud/vivo"))
            .ConfigureAwait(false);

        return respuesta.IsSuccessStatusCode ? 0 : 1;
    }
    catch (Exception excepcion) when (excepcion is HttpRequestException or TaskCanceledException)
    {
        return 1;
    }
}

/// <summary>
/// Punto de entrada del servidor. Se declara explícitamente para que el proyecto
/// de pruebas pueda referenciarlo con <c>WebApplicationFactory</c>.
/// </summary>
public partial class Program;
