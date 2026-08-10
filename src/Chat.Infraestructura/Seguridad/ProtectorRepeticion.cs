using Chat.Aplicacion.Abstracciones;
using Chat.Aplicacion.Opciones;
using Microsoft.Extensions.Options;

namespace Chat.Infraestructura.Seguridad;

/// <summary>
/// Protección básica contra repetición apoyada en la caché: registra cada
/// identificador de operación durante una ventana corta y rechaza los repetidos.
/// </summary>
/// <remarks>
/// No sustituye a la protección que ya ofrecen TLS y la expiración de los tokens;
/// su objetivo es que un mensaje capturado no pueda reinyectarse tal cual y
/// que los reintentos del cliente sean idempotentes.
/// </remarks>
public sealed class ProtectorRepeticion : IProtectorRepeticion
{
    private readonly IServicioCache _cache;
    private readonly CacheOptions _opciones;

    /// <summary>Crea el protector.</summary>
    /// <param name="cache">Caché compartida.</param>
    /// <param name="opciones">Opciones de caché, que definen la ventana temporal.</param>
    public ProtectorRepeticion(IServicioCache cache, IOptions<CacheOptions> opciones)
    {
        _cache = cache;
        _opciones = opciones.Value;
    }

    /// <inheritdoc />
    public async Task<bool> RegistrarSiEsNuevoAsync(
        string ambito,
        string identificador,
        CancellationToken cancelacion = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ambito);
        ArgumentException.ThrowIfNullOrWhiteSpace(identificador);

        var clave = ClavesCache.Repeticion(ambito, identificador);

        if (await _cache.ObtenerAsync<bool>(clave, cancelacion).ConfigureAwait(false))
        {
            return false;
        }

        await _cache
            .EstablecerAsync(clave, true, TimeSpan.FromSeconds(_opciones.SegundosVentanaAntiRepeticion), cancelacion)
            .ConfigureAwait(false);

        return true;
    }
}
