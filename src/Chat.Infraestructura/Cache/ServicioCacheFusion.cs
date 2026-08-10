using Chat.Aplicacion.Abstracciones;
using Chat.Aplicacion.Opciones;
using Microsoft.Extensions.Options;
using ZiggyCreatures.Caching.Fusion;

namespace Chat.Infraestructura.Cache;

/// <summary>
/// Adaptador de <see cref="IServicioCache"/> sobre FusionCache.
/// </summary>
/// <remarks>
/// Las etiquetas se deducen del prefijo de la clave (<c>usuarios:</c>, <c>salas:</c>,
/// <c>configuracion:</c>), de modo que invalidar un agregado completo es una sola
/// llamada y no hace falta llevar un índice de claves aparte.
/// </remarks>
public sealed class ServicioCacheFusion : IServicioCache
{
    private readonly IFusionCache _cache;
    private readonly CacheOptions _opciones;

    /// <summary>Crea el adaptador.</summary>
    /// <param name="cache">Instancia de FusionCache configurada en el arranque.</param>
    /// <param name="opciones">Opciones de caché.</param>
    public ServicioCacheFusion(IFusionCache cache, IOptions<CacheOptions> opciones)
    {
        _cache = cache;
        _opciones = opciones.Value;
    }

    /// <inheritdoc />
    public async Task<T> ObtenerOCrearAsync<T>(
        string clave,
        Func<CancellationToken, Task<T>> generador,
        TimeSpan? duracion = null,
        CancellationToken cancelacion = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clave);
        ArgumentNullException.ThrowIfNull(generador);

        return await _cache.GetOrSetAsync<T>(
            clave,
            (_, ct) => generador(ct),
            options: ConstruirOpciones(duracion),
            tags: DeducirEtiquetas(clave),
            token: cancelacion).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task EstablecerAsync<T>(
        string clave,
        T valor,
        TimeSpan? duracion = null,
        CancellationToken cancelacion = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clave);

        await _cache.SetAsync(
            clave,
            valor,
            ConstruirOpciones(duracion),
            DeducirEtiquetas(clave),
            cancelacion).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<T?> ObtenerAsync<T>(string clave, CancellationToken cancelacion = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clave);

        var resultado = await _cache.TryGetAsync<T>(clave, token: cancelacion).ConfigureAwait(false);
        return resultado.HasValue ? resultado.Value : default;
    }

    /// <inheritdoc />
    public async Task InvalidarAsync(string clave, CancellationToken cancelacion = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clave);
        await _cache.RemoveAsync(clave, token: cancelacion).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task InvalidarPorEtiquetaAsync(string etiqueta, CancellationToken cancelacion = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(etiqueta);
        await _cache.RemoveByTagAsync(etiqueta, token: cancelacion).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task LimpiarTodoAsync(CancellationToken cancelacion = default)
        => await _cache.ClearAsync(token: cancelacion).ConfigureAwait(false);

    /// <summary>Construye las opciones de entrada aplicando la duración pedida o la de por defecto.</summary>
    /// <param name="duracion">Duración solicitada; nula para usar la configurada.</param>
    private FusionCacheEntryOptions ConstruirOpciones(TimeSpan? duracion)
        => new(duracion ?? TimeSpan.FromSeconds(_opciones.SegundosDuracionPorDefecto))
        {
            IsFailSafeEnabled = true,
            FailSafeMaxDuration = TimeSpan.FromSeconds(_opciones.SegundosMargenGracia),
            FailSafeThrottleDuration = TimeSpan.FromSeconds(10),
            FactorySoftTimeout = TimeSpan.FromSeconds(2),
            FactoryHardTimeout = TimeSpan.FromSeconds(10)
        };

    /// <summary>Deduce las etiquetas de una clave a partir de su prefijo.</summary>
    /// <param name="clave">Clave de caché con formato <c>ambito:detalle</c>.</param>
    private static string[]? DeducirEtiquetas(string clave)
    {
        var separador = clave.IndexOf(':', StringComparison.Ordinal);
        if (separador <= 0)
        {
            return null;
        }

        var prefijo = clave[..separador];

        return prefijo switch
        {
            ClavesCache.EtiquetaUsuarios => [ClavesCache.EtiquetaUsuarios],
            ClavesCache.EtiquetaSalas => [ClavesCache.EtiquetaSalas],
            ClavesCache.EtiquetaConfiguracion => [ClavesCache.EtiquetaConfiguracion],
            _ => null
        };
    }
}
