using System.ComponentModel.DataAnnotations;

namespace Chat.Aplicacion.Opciones;

/// <summary>
/// Destino de una de las tres señales de telemetría.
/// </summary>
/// <remarks>
/// Cada señal se configura por separado porque en un despliegue real van a sitios
/// distintos: las trazas a un visor de trazas, los registros a un agregador de
/// registros y las métricas a un sistema de series temporales. Meterlas todas por el
/// mismo punto de entrada obligaría a poner un colector delante solo para repartirlas.
/// </remarks>
public sealed class DestinoTelemetria
{
    /// <summary>Protocolo OTLP sobre gRPC; el receptor suele escuchar en el puerto 4317.</summary>
    public const string ProtocoloGrpc = "grpc";

    /// <summary>Protocolo OTLP sobre HTTP con carga binaria protobuf; puerto 4318.</summary>
    public const string ProtocoloHttp = "http";

    /// <summary>Activa o desactiva esta señal.</summary>
    public bool Activado { get; set; } = true;

    /// <summary>Dirección del receptor OTLP.</summary>
    [Required(ErrorMessage = "El punto de entrada de la señal de telemetría es obligatorio.")]
    public string PuntoEntrada { get; set; } = "http://localhost:4317";

    /// <summary>Protocolo de transporte: <c>grpc</c> o <c>http</c>.</summary>
    [RegularExpression(
        "^(grpc|http)$",
        ErrorMessage = "El protocolo de telemetría debe ser 'grpc' o 'http'.")]
    public string Protocolo { get; set; } = ProtocoloGrpc;

    /// <summary>
    /// Cabeceras adicionales, en el formato <c>clave=valor,clave=valor</c> que define
    /// OTLP. Es por donde viaja la clave de API de Seq.
    /// </summary>
    public string Cabeceras { get; set; } = string.Empty;

    /// <summary>Indica si el transporte configurado es gRPC.</summary>
    public bool EsGrpc => !string.Equals(Protocolo, ProtocoloHttp, StringComparison.OrdinalIgnoreCase);

    /// <summary>Devuelve la dirección del receptor.</summary>
    public Uri ResolverPuntoEntrada() => new(PuntoEntrada);
}

/// <summary>
/// Configuración de la telemetría OpenTelemetry: qué se exporta y a dónde.
/// </summary>
/// <remarks>
/// Los valores por defecto apuntan a los servicios que levanta el
/// <c>docker-compose.yml</c> del repositorio: las trazas y las métricas a Jaeger y los
/// registros a Seq.
/// </remarks>
public sealed class TelemetriaOptions
{
    /// <summary>Nombre de la sección en appsettings.json.</summary>
    public const string Seccion = "Telemetria";

    /// <summary>Activa o desactiva por completo la exportación de telemetría.</summary>
    public bool Activada { get; set; } = true;

    /// <summary>Nombre del servicio con el que aparece la aplicación en los visores.</summary>
    [Required(ErrorMessage = "El nombre del servicio de telemetría es obligatorio.")]
    public string NombreServicio { get; set; } = "dotchat-servidor";

    /// <summary>Entorno de despliegue con el que se etiqueta la telemetría.</summary>
    public string Entorno { get; set; } = "desarrollo";

    /// <summary>Destino de las trazas: peticiones, invocaciones del hub, comandos y consultas.</summary>
    [Required]
    public DestinoTelemetria Trazas { get; set; } = new();

    /// <summary>Destino de las métricas: contadores propios, ASP.NET Core y tiempo de ejecución.</summary>
    [Required]
    public DestinoTelemetria Metricas { get; set; } = new();

    /// <summary>Destino de los registros estructurados.</summary>
    [Required]
    public DestinoTelemetria Registros { get; set; } = new()
    {
        PuntoEntrada = "http://localhost:5341/ingest/otlp/v1/logs",
        Protocolo = DestinoTelemetria.ProtocoloHttp
    };
}
