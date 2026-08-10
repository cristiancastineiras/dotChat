using Chat.Aplicacion.Abstracciones;
using Chat.Aplicacion.Cqrs;
using Chat.Aplicacion.Dtos;
using Microsoft.Extensions.Logging;

namespace Chat.Aplicacion.Comandos.Administracion;

/// <summary>Vacía la caché completa de la plataforma. Operación administrativa.</summary>
/// <param name="SolicitanteId">Administrador que ejecuta la operación.</param>
public sealed record ComandoLimpiarCache(Guid SolicitanteId) : IComando<ResultadoOperacionDto>;

/// <summary>Manejador de <see cref="ComandoLimpiarCache"/>.</summary>
public sealed class ManejadorLimpiarCache : IManejadorComando<ComandoLimpiarCache, ResultadoOperacionDto>
{
    private readonly IServicioCache _cache;
    private readonly ILogger<ManejadorLimpiarCache> _registro;

    /// <summary>Crea el manejador.</summary>
    public ManejadorLimpiarCache(IServicioCache cache, ILogger<ManejadorLimpiarCache> registro)
    {
        _cache = cache;
        _registro = registro;
    }

    /// <inheritdoc />
    public async Task<ResultadoOperacionDto> ManejarAsync(
        ComandoLimpiarCache comando,
        CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(comando);

        await _cache.LimpiarTodoAsync(cancelacion).ConfigureAwait(false);

        _registro.LogInformation("Caché vaciada por completo. SolicitanteId={SolicitanteId}", comando.SolicitanteId);

        return new ResultadoOperacionDto(true, "La caché se ha vaciado por completo.");
    }
}
