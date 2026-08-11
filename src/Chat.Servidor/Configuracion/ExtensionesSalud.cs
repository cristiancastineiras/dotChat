using System.Text.Json;
using Chat.Aplicacion.Abstracciones;
using Chat.Aplicacion.Opciones;
using Chat.Infraestructura.Persistencia;
using Chat.Infraestructura.Presencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Chat.Servidor.Configuracion;

/// <summary>
/// Sondas de salud del servidor.
/// </summary>
/// <remarks>
/// <para>
/// Se publican dos, y la diferencia entre ellas importa cuando hay un balanceador
/// delante:
/// </para>
/// <list type="bullet">
///   <item><description>
///     <c>/salud/vivo</c> responde en cuanto el proceso está en pie. Es lo que mira el
///     orquestador para decidir si hay que reiniciar el contenedor, y por eso no
///     comprueba dependencias: que PostgreSQL esté caído no se arregla reiniciando la
///     aplicación una y otra vez.
///   </description></item>
///   <item><description>
///     <c>/salud/listo</c> comprueba la base de datos, Valkey y el almacén de objetos.
///     Es lo que mira el balanceador para decidir si mandarle tráfico a esta réplica.
///   </description></item>
/// </list>
/// </remarks>
public static class ExtensionesSalud
{
    /// <summary>Etiqueta de las comprobaciones que deciden si la réplica acepta tráfico.</summary>
    public const string EtiquetaListo = "listo";

    /// <summary>Registra las comprobaciones de salud.</summary>
    /// <param name="servicios">Colección de servicios.</param>
    /// <param name="valkeyActivado">Indica si el nivel distribuido está en uso.</param>
    /// <returns>La misma colección, para encadenar llamadas.</returns>
    public static IServiceCollection AgregarSalud(this IServiceCollection servicios, bool valkeyActivado)
    {
        ArgumentNullException.ThrowIfNull(servicios);

        var constructor = servicios.AddHealthChecks()
            .AddCheck<ComprobacionBaseDatos>("base-datos", tags: [EtiquetaListo])
            .AddCheck<ComprobacionAlmacenObjetos>("almacen-objetos", tags: [EtiquetaListo]);

        if (valkeyActivado)
        {
            constructor.AddCheck<ComprobacionValkey>("valkey", tags: [EtiquetaListo]);
        }

        return servicios;
    }

    /// <summary>Publica las rutas de salud.</summary>
    /// <param name="aplicacion">Aplicación web.</param>
    /// <returns>La misma aplicación, para encadenar llamadas.</returns>
    public static WebApplication MapearSalud(this WebApplication aplicacion)
    {
        ArgumentNullException.ThrowIfNull(aplicacion);

        aplicacion.MapHealthChecks("/salud/vivo", new()
        {
            // Ninguna comprobación: basta con que el proceso conteste.
            Predicate = _ => false
        }).AllowAnonymous();

        aplicacion.MapHealthChecks("/salud/listo", new()
        {
            Predicate = comprobacion => comprobacion.Tags.Contains(EtiquetaListo),
            ResponseWriter = EscribirRespuestaAsync
        }).AllowAnonymous();

        return aplicacion;
    }

    /// <summary>
    /// Escribe el detalle de cada comprobación. Es información de diagnóstico de una
    /// ruta anónima, así que se publica el estado y la duración, nunca el mensaje de
    /// la excepción, que podría revelar cadenas de conexión o rutas internas.
    /// </summary>
    /// <param name="contexto">Contexto HTTP.</param>
    /// <param name="informe">Resultado de las comprobaciones.</param>
    private static async Task EscribirRespuestaAsync(HttpContext contexto, HealthReport informe)
    {
        contexto.Response.ContentType = "application/json; charset=utf-8";

        var salida = new
        {
            estado = informe.Status.ToString(),
            duracionMs = (int)informe.TotalDuration.TotalMilliseconds,
            replica = contexto.RequestServices.GetRequiredService<IdentidadReplica>().Nombre,
            comprobaciones = informe.Entries.ToDictionary(
                entrada => entrada.Key,
                entrada => new
                {
                    estado = entrada.Value.Status.ToString(),
                    duracionMs = (int)entrada.Value.Duration.TotalMilliseconds
                })
        };

        await contexto.Response
            .WriteAsync(JsonSerializer.Serialize(salida, OpcionesJson))
            .ConfigureAwait(false);
    }

    /// <summary>Opciones de serialización del informe de salud.</summary>
    private static readonly JsonSerializerOptions OpcionesJson = new(JsonSerializerDefaults.Web);
}

/// <summary>Comprueba que la base de datos responde.</summary>
public sealed class ComprobacionBaseDatos : IHealthCheck
{
    private readonly ContextoChat _contexto;

    /// <summary>Crea la comprobación.</summary>
    /// <param name="contexto">Contexto de EF Core.</param>
    public ComprobacionBaseDatos(ContextoChat contexto) => _contexto = contexto;

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext contexto,
        CancellationToken cancelacion = default)
    {
        try
        {
            return await _contexto.Database.CanConnectAsync(cancelacion).ConfigureAwait(false)
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("La base de datos no acepta conexiones.");
        }
        catch (Exception excepcion) when (excepcion is not OperationCanceledException)
        {
            return HealthCheckResult.Unhealthy("La base de datos no responde.", excepcion);
        }
    }
}

/// <summary>Comprueba que Valkey responde.</summary>
public sealed class ComprobacionValkey : IHealthCheck
{
    private readonly IConnectionMultiplexer _conexion;
    private readonly ValkeyOptions _opciones;

    /// <summary>Crea la comprobación.</summary>
    /// <param name="conexion">Conexión compartida con Valkey.</param>
    /// <param name="opciones">Opciones de Valkey.</param>
    public ComprobacionValkey(IConnectionMultiplexer conexion, IOptions<ValkeyOptions> opciones)
    {
        ArgumentNullException.ThrowIfNull(opciones);

        _conexion = conexion;
        _opciones = opciones.Value;
    }

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext contexto,
        CancellationToken cancelacion = default)
    {
        try
        {
            var latencia = await _conexion.GetDatabase().PingAsync().ConfigureAwait(false);

            // Una Valkey que responde pero lentísima es peor que una caída: deja las
            // peticiones colgando en lugar de fallar rápido.
            return latencia.TotalMilliseconds < _opciones.MilisegundosTiempoEspera
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Degraded($"Valkey responde en {latencia.TotalMilliseconds:0} ms.");
        }
        catch (Exception excepcion) when (excepcion is not OperationCanceledException)
        {
            return HealthCheckResult.Unhealthy("Valkey no responde.", excepcion);
        }
    }
}

/// <summary>Comprueba que el almacén de objetos responde y tiene su contenedor.</summary>
public sealed class ComprobacionAlmacenObjetos : IHealthCheck
{
    private readonly IAlmacenObjetos _almacen;

    /// <summary>Crea la comprobación.</summary>
    /// <param name="almacen">Almacén de objetos.</param>
    public ComprobacionAlmacenObjetos(IAlmacenObjetos almacen) => _almacen = almacen;

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext contexto,
        CancellationToken cancelacion = default)
        => await _almacen.RespondeAsync(cancelacion).ConfigureAwait(false)
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy("El almacén de objetos no responde o le falta el contenedor.");
}
