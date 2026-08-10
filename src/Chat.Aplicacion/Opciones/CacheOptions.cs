using System.ComponentModel.DataAnnotations;

namespace Chat.Aplicacion.Opciones;

/// <summary>Configuración de la caché distribuida en memoria (FusionCache).</summary>
public sealed class CacheOptions
{
    /// <summary>Nombre de la sección en appsettings.json.</summary>
    public const string Seccion = "Cache";

    /// <summary>Duración por defecto de las entradas, en segundos.</summary>
    [Range(1, 86400, ErrorMessage = "La duración por defecto debe estar entre 1 y 86400 segundos.")]
    public int SegundosDuracionPorDefecto { get; set; } = 60;

    /// <summary>Duración de las entradas de usuarios, en segundos.</summary>
    [Range(1, 86400, ErrorMessage = "La duración de usuarios debe estar entre 1 y 86400 segundos.")]
    public int SegundosDuracionUsuarios { get; set; } = 120;

    /// <summary>Duración de las entradas de salas, en segundos.</summary>
    [Range(1, 86400, ErrorMessage = "La duración de salas debe estar entre 1 y 86400 segundos.")]
    public int SegundosDuracionSalas { get; set; } = 300;

    /// <summary>Duración de las entradas de configuración, en segundos.</summary>
    [Range(1, 86400, ErrorMessage = "La duración de configuración debe estar entre 1 y 86400 segundos.")]
    public int SegundosDuracionConfiguracion { get; set; } = 900;

    /// <summary>
    /// Margen de gracia («fail-safe») durante el cual FusionCache puede servir un valor
    /// caducado si la fuente original falla, expresado en segundos.
    /// </summary>
    [Range(0, 604800, ErrorMessage = "El margen de gracia debe estar entre 0 y 604800 segundos.")]
    public int SegundosMargenGracia { get; set; } = 3600;

    /// <summary>Ventana, en segundos, durante la que se recuerdan los identificadores de mensaje para evitar repeticiones.</summary>
    [Range(5, 3600, ErrorMessage = "La ventana antirrepetición debe estar entre 5 y 3600 segundos.")]
    public int SegundosVentanaAntiRepeticion { get; set; } = 120;
}
