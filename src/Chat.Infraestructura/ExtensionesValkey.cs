using Chat.Aplicacion.Abstracciones;
using Chat.Aplicacion.Opciones;
using Chat.Infraestructura.Cache;
using Chat.Infraestructura.Presencia;
using Chat.Infraestructura.Seguridad;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Backplane.StackExchangeRedis;
using ZiggyCreatures.Caching.Fusion.Serialization.SystemTextJson;

namespace Chat.Infraestructura;

/// <summary>
/// Registro de todo lo que se apoya en Valkey: la caché de segundo nivel, la presencia
/// compartida y el limitador de envíos.
/// </summary>
/// <remarks>
/// <para>
/// Valkey es la pieza que convierte varias instancias del servidor en un clúster. Sin
/// ella cada réplica tendría su propia caché, su propia idea de quién está conectado y
/// su propio contador de mensajes por minuto, y el comportamiento del sistema
/// dependería de a qué nodo hubiera caído cada petición.
/// </para>
/// <para>
/// Toda la aplicación comparte <b>una sola conexión</b>. El cliente multiplexa las
/// órdenes de todos los usos sobre el mismo par de sockets, así que abrir una por
/// componente solo añadiría descriptores y latencia de reconexión.
/// </para>
/// </remarks>
public static class ExtensionesValkey
{
    /// <summary>
    /// Registra la conexión compartida y todo lo que cuelga de ella.
    /// </summary>
    /// <param name="servicios">Colección de servicios.</param>
    /// <param name="configuracion">Configuración de la aplicación.</param>
    /// <returns>Las opciones resueltas, que el servidor necesita para el resto del cableado.</returns>
    public static ValkeyOptions AgregarValkey(
        this IServiceCollection servicios,
        IConfiguration configuracion)
    {
        ArgumentNullException.ThrowIfNull(servicios);
        ArgumentNullException.ThrowIfNull(configuracion);

        var opciones = configuracion.GetSection(ValkeyOptions.Seccion).Get<ValkeyOptions>() ?? new ValkeyOptions();

        servicios.AddSingleton<IdentidadReplica>();

        if (!opciones.Activado)
        {
            // Sin Valkey el servidor sigue siendo utilizable, pero en una sola
            // instancia: la caché es local y la presencia vive en memoria.
            servicios.AgregarCacheLocal();
            servicios.AddSingleton<IRegistroConexiones, RegistroConexionesMemoria>();
            servicios.AddSingleton<ILimitadorEnvios, LimitadorEnviosMemoria>();

            return opciones;
        }

        servicios.AddSingleton<IConnectionMultiplexer>(proveedor =>
        {
            var registro = proveedor.GetRequiredService<ILogger<ConnectionMultiplexer>>();
            var conexion = ConnectionMultiplexer.Connect(ConstruirConfiguracion(opciones));

            // Las caídas y recuperaciones se registran: si la presencia se comporta de
            // forma rara, lo primero que hay que saber es si el canal se cortó.
            conexion.ConnectionFailed += (_, argumentos) => registro.LogWarning(
                argumentos.Exception,
                "Conexión con Valkey perdida. Tipo={Tipo} Punto={Punto}",
                argumentos.ConnectionType,
                argumentos.EndPoint);

            conexion.ConnectionRestored += (_, argumentos) => registro.LogInformation(
                "Conexión con Valkey restablecida. Tipo={Tipo} Punto={Punto}",
                argumentos.ConnectionType,
                argumentos.EndPoint);

            return conexion;
        });

        servicios.AgregarCacheDistribuida(opciones);
        servicios.AddSingleton<IRegistroConexiones, RegistroConexionesValkey>();
        servicios.AddSingleton<ILimitadorEnvios, LimitadorEnviosValkey>();

        return opciones;
    }

    /// <summary>
    /// Compone la configuración del cliente a partir de las opciones de la aplicación.
    /// </summary>
    /// <param name="opciones">Opciones de Valkey.</param>
    public static ConfigurationOptions ConstruirConfiguracion(ValkeyOptions opciones)
    {
        ArgumentNullException.ThrowIfNull(opciones);

        var configuracion = ConfigurationOptions.Parse(opciones.Conexion);

        configuracion.ConnectTimeout = opciones.MilisegundosTiempoEspera;
        configuracion.SyncTimeout = opciones.MilisegundosTiempoEspera;
        configuracion.ClientName = "dotchat";

        // Arrancar sin Valkey levantada no debe impedir que el servidor arranque: el
        // cliente reintenta en segundo plano y las operaciones fallan mientras tanto,
        // que es un fallo mucho más manejable que no arrancar.
        configuracion.AbortOnConnectFail = false;

        return configuracion;
    }

    /// <summary>Registra la caché en dos niveles, con Valkey como segundo.</summary>
    /// <param name="servicios">Colección de servicios.</param>
    /// <param name="opciones">Opciones de Valkey.</param>
    private static void AgregarCacheDistribuida(this IServiceCollection servicios, ValkeyOptions opciones)
    {
        servicios.AddStackExchangeRedisCache(redis =>
        {
            redis.ConfigurationOptions = ConstruirConfiguracion(opciones);
            redis.InstanceName = opciones.PrefijoClaves();
        });

        servicios.ConstruirCacheBase(opciones)
            // El serializador convierte los valores cacheados a JSON: es el formato
            // que viaja a Valkey y el que se puede inspeccionar a mano.
            .WithSerializer(new FusionCacheSystemTextJsonSerializer())
            .WithRegisteredDistributedCache()
            .WithBackplane(new RedisBackplane(new RedisBackplaneOptions
            {
                ConfigurationOptions = ConstruirConfiguracion(opciones)
            }))
            .WithDefaultEntryOptions(entrada =>
            {
                // Si Valkey tarda más de lo previsto, se responde con el valor de
                // memoria y la escritura al segundo nivel se completa en segundo plano.
                entrada.DistributedCacheSoftTimeout =
                    TimeSpan.FromMilliseconds(opciones.MilisegundosMargenBlando);
                entrada.DistributedCacheHardTimeout =
                    TimeSpan.FromMilliseconds(opciones.MilisegundosTiempoEspera);
                entrada.AllowBackgroundDistributedCacheOperations = true;
                entrada.AllowBackgroundBackplaneOperations = true;
            });

        servicios.AddSingleton<IServicioCache, ServicioCacheFusion>();
    }

    /// <summary>Registra la caché reducida a la memoria del proceso.</summary>
    /// <param name="servicios">Colección de servicios.</param>
    private static void AgregarCacheLocal(this IServiceCollection servicios)
    {
        servicios.ConstruirCacheBase(new ValkeyOptions());
        servicios.AddSingleton<IServicioCache, ServicioCacheFusion>();
    }

    /// <summary>Configura la parte de FusionCache que es común a los dos modos.</summary>
    /// <param name="servicios">Colección de servicios.</param>
    /// <param name="opciones">Opciones de Valkey.</param>
    private static IFusionCacheBuilder ConstruirCacheBase(
        this IServiceCollection servicios,
        ValkeyOptions opciones)
        => servicios.AddFusionCache()
            .WithOptions(fusion =>
            {
                // Un fallo de la caché nunca debe tumbar una petición.
                fusion.DefaultEntryOptions = new FusionCacheEntryOptions(TimeSpan.FromMinutes(1))
                {
                    IsFailSafeEnabled = true,
                    FailSafeMaxDuration = TimeSpan.FromHours(1)
                };

                // Los errores del segundo nivel y del canal se registran, pero no se
                // relanzan: la caché es un acelerador, no una dependencia dura.
                fusion.DistributedCacheCircuitBreakerDuration = TimeSpan.FromSeconds(30);
                fusion.BackplaneCircuitBreakerDuration = TimeSpan.FromSeconds(30);

                // El prefijo aísla el canal de notificaciones igual que aísla las
                // claves: dos entornos sobre la misma Valkey no se invalidan entre sí.
                fusion.BackplaneChannelPrefix = opciones.CanalRetropropagacion();
            });
}
