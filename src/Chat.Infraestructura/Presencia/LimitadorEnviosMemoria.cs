using System.Collections.Concurrent;
using System.Threading.RateLimiting;
using Chat.Aplicacion.Abstracciones;
using Chat.Aplicacion.Opciones;
using Microsoft.Extensions.Options;

namespace Chat.Infraestructura.Presencia;

/// <summary>
/// Limitador de envíos local al proceso, para cuando Valkey está desactivado y el
/// servidor corre en una sola instancia.
/// </summary>
public sealed class LimitadorEnviosMemoria : ILimitadorEnvios, IDisposable
{
    private readonly ConcurrentDictionary<Guid, RateLimiter> _limitadores = new();
    private readonly SignalROptions _opciones;
    private bool _liberado;

    /// <summary>Crea el limitador.</summary>
    /// <param name="opciones">Opciones de SignalR, que definen el máximo por minuto.</param>
    public LimitadorEnviosMemoria(IOptions<SignalROptions> opciones)
    {
        ArgumentNullException.ThrowIfNull(opciones);
        _opciones = opciones.Value;
    }

    /// <inheritdoc />
    public Task<bool> IntentarConsumirAsync(Guid usuarioId, CancellationToken cancelacion = default)
    {
        ObjectDisposedException.ThrowIf(_liberado, this);

        var limitador = _limitadores.GetOrAdd(usuarioId, _ => new FixedWindowRateLimiter(
            new FixedWindowRateLimiterOptions
            {
                PermitLimit = _opciones.MaximoMensajesPorMinuto,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                AutoReplenishment = true
            }));

        using var permiso = limitador.AttemptAcquire();
        return Task.FromResult(permiso.IsAcquired);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_liberado)
        {
            return;
        }

        foreach (var limitador in _limitadores.Values)
        {
            limitador.Dispose();
        }

        _limitadores.Clear();
        _liberado = true;
    }
}
