using System.ComponentModel.DataAnnotations;

namespace Chat.Aplicacion.Opciones;

/// <summary>
/// Configuración de la telemetría OpenTelemetry: qué se exporta y a dónde.
/// Los valores por defecto apuntan al receptor local que escucha OpenTelemetry
/// Desktop Viewer, de modo que basta con arrancar el visor para ver las trazas.
/// </summary>
public sealed class TelemetriaOptions
{
    /// <summary>Nombre de la sección en appsettings.json.</summary>
    public const string Seccion = "Telemetria";

    /// <summary>Protocolo OTLP sobre gRPC; el receptor escucha en el puerto 4317.</summary>
    public const string ProtocoloGrpc = "grpc";

    /// <summary>Protocolo OTLP sobre HTTP con carga binaria protobuf; puerto 4318.</summary>
    public const string ProtocoloHttp = "http";

    /// <summary>Punto de entrada por defecto para gRPC.</summary>
    public const string PuntoEntradaGrpc = "http://localhost:4317";

    /// <summary>Punto de entrada por defecto para HTTP.</summary>
    public const string PuntoEntradaHttp = "http://localhost:4318";

    /// <summary>Activa o desactiva por completo la exportación de telemetría.</summary>
    public bool Activada { get; set; } = true;

    /// <summary>
    /// Dirección del receptor OTLP. Si se deja vacía se usa la que corresponda al
    /// protocolo elegido.
    /// </summary>
    public string PuntoEntrada { get; set; } = string.Empty;

    /// <summary>Protocolo de transporte: <c>grpc</c> (4317) o <c>http</c> (4318).</summary>
    [RegularExpression(
        "^(grpc|http)$",
        ErrorMessage = "El protocolo de telemetría debe ser 'grpc' o 'http'.")]
    public string Protocolo { get; set; } = ProtocoloGrpc;

    /// <summary>Nombre del servicio con el que aparece la aplicación en el visor.</summary>
    [Required(ErrorMessage = "El nombre del servicio de telemetría es obligatorio.")]
    public string NombreServicio { get; set; } = "dotchat-servidor";

    /// <summary>Exporta trazas (peticiones HTTP, invocaciones del hub, comandos y consultas).</summary>
    public bool Trazas { get; set; } = true;

    /// <summary>Exporta métricas (contadores propios, ASP.NET Core y tiempo de ejecución).</summary>
    public bool Metricas { get; set; } = true;

    /// <summary>Exporta los registros de la aplicación como logs OTLP.</summary>
    public bool Registros { get; set; } = true;

    /// <summary>Devuelve la dirección efectiva del receptor, aplicando el valor por defecto.</summary>
    public Uri ResolverPuntoEntrada()
    {
        if (!string.IsNullOrWhiteSpace(PuntoEntrada))
        {
            return new Uri(PuntoEntrada);
        }

        return new Uri(EsGrpc ? PuntoEntradaGrpc : PuntoEntradaHttp);
    }

    /// <summary>Indica si el transporte configurado es gRPC.</summary>
    public bool EsGrpc => !string.Equals(Protocolo, ProtocoloHttp, StringComparison.OrdinalIgnoreCase);
}
