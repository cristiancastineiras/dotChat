using Chat.Aplicacion.Observabilidad;
using Chat.Aplicacion.Opciones;
using Chat.Servidor.Observabilidad;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Chat.Servidor.Configuracion;

/// <summary>
/// Configuración de OpenTelemetry. Exporta trazas, métricas y registros por OTLP
/// hacia un receptor local —por ejemplo OpenTelemetry Desktop Viewer— sin que el
/// resto del código tenga que saber nada de ello.
/// </summary>
public static class ExtensionesTelemetria
{
    /// <summary>
    /// Registra los tres proveedores de telemetría y su exportador OTLP.
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

        var puntoEntrada = opciones.ResolverPuntoEntrada();
        var protocolo = opciones.EsGrpc ? OtlpExportProtocol.Grpc : OtlpExportProtocol.HttpProtobuf;

        void ConfigurarExportador(OtlpExporterOptions exportador)
        {
            exportador.Endpoint = puntoEntrada;
            exportador.Protocol = protocolo;
        }

        var telemetria = constructor.Services.AddOpenTelemetry()
            .ConfigureResource(recurso => recurso.AddService(
                serviceName: opciones.NombreServicio,
                serviceVersion: typeof(ExtensionesTelemetria).Assembly.GetName().Version?.ToString(),
                serviceInstanceId: Environment.MachineName));

        if (opciones.Trazas)
        {
            telemetria.WithTracing(trazas => trazas
                .AddSource(MedidasChat.NombreFuente)
                .AddSource(TrazasAplicacion.NombreFuente)
                .AddAspNetCoreInstrumentation(aspnet =>
                {
                    aspnet.RecordException = true;

                    // La negociación de SignalR y los sondeos de estado generan mucho
                    // ruido y ninguna información útil: se dejan fuera de las trazas.
                    aspnet.Filter = contexto =>
                        !contexto.Request.Path.StartsWithSegments("/api/estado");
                })
                .AddHttpClientInstrumentation()
                .AddOtlpExporter(ConfigurarExportador));
        }

        if (opciones.Metricas)
        {
            telemetria.WithMetrics(metricas => metricas
                .AddMeter(MedidasChat.NombreMedidor)
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddOtlpExporter(ConfigurarExportador));
        }

        if (opciones.Registros)
        {
            telemetria.WithLogging(registros => registros.AddOtlpExporter(ConfigurarExportador));
        }

        return constructor;
    }
}
