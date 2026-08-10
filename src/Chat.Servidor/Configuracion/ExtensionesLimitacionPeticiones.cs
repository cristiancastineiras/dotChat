using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace Chat.Servidor.Configuracion;

/// <summary>
/// Limitación de peticiones basada en el middleware nativo de ASP.NET Core.
/// Reduce la superficie de ataque frente a fuerza bruta sobre el login y frente a
/// inundaciones de peticiones desde un mismo origen.
/// </summary>
public static class ExtensionesLimitacionPeticiones
{
    /// <summary>Política estricta aplicada a los endpoints de autenticación.</summary>
    public const string PoliticaAutenticacion = "limite-autenticacion";

    /// <summary>Política general aplicada al resto de la API.</summary>
    public const string PoliticaApi = "limite-api";

    /// <summary>Registra las políticas de limitación de peticiones.</summary>
    /// <param name="servicios">Colección de servicios.</param>
    /// <returns>La misma colección, para encadenar llamadas.</returns>
    public static IServiceCollection AgregarLimitacionPeticiones(this IServiceCollection servicios)
    {
        ArgumentNullException.ThrowIfNull(servicios);

        servicios.AddRateLimiter(opciones =>
        {
            opciones.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // Autenticación: ventana fija corta y estrecha, particionada por IP.
            opciones.AddPolicy(PoliticaAutenticacion, contexto =>
                RateLimitPartition.GetFixedWindowLimiter(
                    ObtenerParticion(contexto),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 10,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst
                    }));

            // API general: ventana deslizante más holgada, particionada por usuario o IP.
            opciones.AddPolicy(PoliticaApi, contexto =>
                RateLimitPartition.GetSlidingWindowLimiter(
                    ObtenerParticion(contexto),
                    _ => new SlidingWindowRateLimiterOptions
                    {
                        PermitLimit = 120,
                        Window = TimeSpan.FromMinutes(1),
                        SegmentsPerWindow = 6,
                        QueueLimit = 0,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst
                    }));

            opciones.OnRejected = async (contexto, cancelacion) =>
            {
                if (contexto.Lease.TryGetMetadata(MetadataName.RetryAfter, out var reintentar))
                {
                    contexto.HttpContext.Response.Headers.RetryAfter =
                        ((int)reintentar.TotalSeconds).ToString(CultureInfo.InvariantCulture);
                }

                contexto.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;

                await contexto.HttpContext.Response
                    .WriteAsJsonAsync(
                        new { error = "Se ha superado el límite de peticiones. Inténtelo de nuevo en unos instantes." },
                        cancelacion)
                    .ConfigureAwait(false);
            };
        });

        return servicios;
    }

    /// <summary>
    /// Calcula la clave de partición: el usuario autenticado si lo hay, y si no la
    /// dirección IP remota. Así un abuso puntual no afecta a los demás clientes.
    /// </summary>
    /// <param name="contexto">Contexto HTTP de la petición.</param>
    private static string ObtenerParticion(HttpContext contexto)
    {
        if (contexto.User.Identity?.IsAuthenticated == true)
        {
            return $"usuario:{contexto.User.ObtenerUsuarioId()}";
        }

        return $"ip:{contexto.Connection.RemoteIpAddress?.ToString() ?? "desconocida"}";
    }
}
