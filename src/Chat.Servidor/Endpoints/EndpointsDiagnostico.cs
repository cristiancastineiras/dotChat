using Chat.Aplicacion.Abstracciones;

namespace Chat.Servidor.Endpoints;

/// <summary>Endpoints públicos de diagnóstico y descubrimiento de configuración.</summary>
public static class EndpointsDiagnostico
{
    /// <summary>Registra los endpoints de estado y configuración pública.</summary>
    /// <param name="rutas">Constructor de rutas.</param>
    /// <returns>El mismo constructor, para encadenar llamadas.</returns>
    public static IEndpointRouteBuilder MapearEndpointsDiagnostico(this IEndpointRouteBuilder rutas)
    {
        ArgumentNullException.ThrowIfNull(rutas);

        rutas.MapGet("/api/estado", (IProveedorFechaHora reloj) => Results.Ok(new
        {
            estado = "activo",
            version = typeof(EndpointsDiagnostico).Assembly.GetName().Version?.ToString() ?? "desconocida",
            fechaServidor = reloj.Ahora
        }))
        .WithTags("Diagnóstico")
        .WithName("ObtenerEstado")
        .WithSummary("Comprobación de vida del servidor.")
        .AllowAnonymous();

        rutas.MapGet("/api/configuracion", async (
                IServicioConfiguracionPlataforma servicio,
                CancellationToken cancelacion) =>
            Results.Ok(await servicio.ObtenerAsync(cancelacion).ConfigureAwait(false)))
        .WithTags("Diagnóstico")
        .WithName("ObtenerConfiguracionPublica")
        .WithSummary("Configuración pública que los clientes leen al arrancar.")
        .AllowAnonymous();

        return rutas;
    }
}
