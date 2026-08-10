using Chat.Aplicacion.Abstracciones;
using Chat.Aplicacion.Opciones;
using Microsoft.Extensions.Options;

namespace Chat.Aplicacion.Servicios;

/// <summary>
/// Sirve la configuración pública de la plataforma desde la caché.
/// Es el tercer conjunto de datos cacheado, junto con usuarios y salas.
/// </summary>
public sealed class ServicioConfiguracionPlataforma : IServicioConfiguracionPlataforma
{
    private readonly IServicioCache _cache;
    private readonly SignalROptions _signalR;
    private readonly CifradoOptions _cifrado;
    private readonly JwtOptions _jwt;
    private readonly CacheOptions _opcionesCache;

    /// <summary>Crea el servicio.</summary>
    public ServicioConfiguracionPlataforma(
        IServicioCache cache,
        IOptions<SignalROptions> signalR,
        IOptions<CifradoOptions> cifrado,
        IOptions<JwtOptions> jwt,
        IOptions<CacheOptions> opcionesCache)
    {
        _cache = cache;
        _signalR = signalR.Value;
        _cifrado = cifrado.Value;
        _jwt = jwt.Value;
        _opcionesCache = opcionesCache.Value;
    }

    /// <inheritdoc />
    public Task<ConfiguracionPlataformaDto> ObtenerAsync(CancellationToken cancelacion = default)
        => _cache.ObtenerOCrearAsync(
            ClavesCache.ConfiguracionPlataforma,
            _ => Task.FromResult(new ConfiguracionPlataformaDto(
                _signalR.RutaHub,
                _cifrado.LongitudMaximaMensaje,
                _jwt.MinutosVigenciaAcceso,
                _signalR.MaximoMensajesPorMinuto)),
            TimeSpan.FromSeconds(_opcionesCache.SegundosDuracionConfiguracion),
            cancelacion);
}
