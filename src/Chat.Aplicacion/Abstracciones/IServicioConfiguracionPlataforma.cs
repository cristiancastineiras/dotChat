namespace Chat.Aplicacion.Abstracciones;

/// <summary>Configuración pública que el servidor comparte con los clientes.</summary>
/// <param name="RutaHub">Ruta relativa del hub de SignalR.</param>
/// <param name="LongitudMaximaMensaje">Longitud máxima admitida para un mensaje.</param>
/// <param name="MinutosVigenciaAcceso">Minutos de validez del token de acceso.</param>
/// <param name="MaximoMensajesPorMinuto">Límite de envíos por usuario y minuto.</param>
public sealed record ConfiguracionPlataformaDto(
    string RutaHub,
    int LongitudMaximaMensaje,
    int MinutosVigenciaAcceso,
    int MaximoMensajesPorMinuto);

/// <summary>
/// Expone la configuración pública de la plataforma. Se sirve desde caché porque
/// cambia con muy poca frecuencia y la consultan todos los clientes al arrancar.
/// </summary>
public interface IServicioConfiguracionPlataforma
{
    /// <summary>Obtiene la configuración pública, apoyándose en la caché.</summary>
    /// <param name="cancelacion">Token de cancelación.</param>
    Task<ConfiguracionPlataformaDto> ObtenerAsync(CancellationToken cancelacion = default);
}
