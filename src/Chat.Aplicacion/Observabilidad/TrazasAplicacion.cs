using System.Diagnostics;
using System.Reflection;

namespace Chat.Aplicacion.Observabilidad;

/// <summary>
/// Fuente de trazas de la capa de aplicación. Se apoya únicamente en
/// <see cref="ActivitySource"/>, que forma parte de la biblioteca base, de modo que
/// esta capa se instrumenta sin depender de ningún paquete de OpenTelemetry: quien
/// decide qué hacer con las actividades es el proceso anfitrión.
/// </summary>
public static class TrazasAplicacion
{
    /// <summary>Nombre de la fuente; hay que registrarlo en el proveedor de trazas.</summary>
    public const string NombreFuente = "Chat.Aplicacion";

    /// <summary>Fuente de actividades de la capa de aplicación.</summary>
    public static ActivitySource Fuente { get; } = new(
        NombreFuente,
        Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0");
}
