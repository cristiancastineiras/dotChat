using Chat.Aplicacion.Observabilidad;
using Chat.Aplicacion.Opciones;
using Chat.Infraestructura.Presencia;
using Chat.Servidor.Observabilidad;
using Npgsql;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Chat.Servidor.Configuracion;

/// <summary>
/// Configuración de OpenTelemetry. Exporta cada señal a su destino: las trazas y las
/// métricas a Jaeger y los registros a Seq, sin que el resto del código tenga que
/// saber nada de ello.
/// </summary>
public static class ExtensionesTelemetria
{
    /// <summary>Rutas que no aportan nada a las trazas y sí mucho ruido.</summary>
    private static readonly string[] RutasIgnoradas = ["/api/estado", "/salud/vivo", "/salud/listo"];

    /// <summary>
    /// Registra los tres proveedores de telemetría y sus exportadores.
    /// </summary>
    /// <param name="constructor">Constructor de la aplicación web.</param>
    /// <returns>El mismo constructor, para encadenar llamadas.</returns>
    public static WebApplicationBuilder AgregarTelemetria(this WebApplicationBuilder constructor)
    {
        ArgumentNullException.ThrowIfNull(constructor);

        constructor.Services.AddOptions<TelemetriaOptions>()
            .Bind(constructor.Configuration.GetSection(TelemetriaOptions.Seccion))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        var opciones = constructor.Configuration
            .GetSection(TelemetriaOptions.Seccion)
            .Get<TelemetriaOptions>() ?? new TelemetriaOptions();

        if (!opciones.Activada)
        {
            return constructor;
        }

        // La identidad de la réplica va en el recurso, así que la lleva cada traza,
        // cada métrica y cada registro. Es lo que permite responder «¿qué instancia
        // atendió esto?» sin tener que correlacionar a mano.
        var identidad = new IdentidadReplica();

        var telemetria = constructor.Services.AddOpenTelemetry()
            .ConfigureResource(recurso => recurso
                .AddService(
                    serviceName: opciones.NombreServicio,
                    serviceVersion: typeof(ExtensionesTelemetria).Assembly.GetName().Version?.ToString(),
                    serviceInstanceId: identidad.Id)
                .AddAttributes(
                [
                    new KeyValuePair<string, object>("deployment.environment", opciones.Entorno),
                    new KeyValuePair<string, object>("dotchat.replica", identidad.Nombre)
                ]));

        if (opciones.Trazas.Activado)
        {
            telemetria.WithTracing(trazas => trazas
                .AddSource(MedidasChat.NombreFuente)
                .AddSource(TrazasAplicacion.NombreFuente)
                .AddAspNetCoreInstrumentation(aspnet =>
                {
                    aspnet.RecordException = true;

                    // Los sondeos de salud y de estado se repiten cada pocos segundos
                    // por cada réplica: ahogarían cualquier traza que interese.
                    aspnet.Filter = contexto => !EsRutaIgnorada(contexto.Request.Path);
                })
                .AddHttpClientInstrumentation()

                // Npgsql emite una actividad por consulta, con el texto del comando ya
                // parametrizado: es lo que convierte «la petición tardó un segundo» en
                // «la consulta de mensajes tardó un segundo».
                .AddNpgsql()
                .AddOtlpExporter(exportador => Configurar(exportador, opciones.Trazas)));
        }

        if (opciones.Metricas.Activado)
        {
            telemetria.WithMetrics(metricas => metricas
                .AddMeter(MedidasChat.NombreMedidor)
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddOtlpExporter(exportador => Configurar(exportador, opciones.Metricas)));
        }

        if (opciones.Registros.Activado)
        {
            constructor.Logging.AddOpenTelemetry(registros =>
            {
                // Sin esto, Seq recibiría el mensaje ya compuesto y se perdería la
                // posibilidad de filtrar por los valores de la plantilla, que es
                // justamente lo que hace útil un registro estructurado.
                registros.IncludeFormattedMessage = true;
                registros.IncludeScopes = true;
                registros.ParseStateValues = true;

                registros.AddOtlpExporter(exportador => Configurar(exportador, opciones.Registros));
            });
        }

        return constructor;
    }

    /// <summary>Aplica a un exportador el destino configurado para su señal.</summary>
    /// <param name="exportador">Exportador a configurar.</param>
    /// <param name="destino">Destino de la señal.</param>
    private static void Configurar(OtlpExporterOptions exportador, DestinoTelemetria destino)
    {
        exportador.Endpoint = destino.ResolverPuntoEntrada();
        exportador.Protocol = destino.EsGrpc ? OtlpExportProtocol.Grpc : OtlpExportProtocol.HttpProtobuf;

        if (!string.IsNullOrWhiteSpace(destino.Cabeceras))
        {
            exportador.Headers = destino.Cabeceras;
        }
    }

    /// <summary>Indica si una ruta queda fuera de las trazas.</summary>
    /// <param name="ruta">Ruta de la petición.</param>
    private static bool EsRutaIgnorada(PathString ruta)
    {
        foreach (var ignorada in RutasIgnoradas)
        {
            if (ruta.StartsWithSegments(ignorada, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
