using System.Collections.Concurrent;
using System.Threading.RateLimiting;
using Chat.Aplicacion.Opciones;
using Microsoft.Extensions.Options;

namespace Chat.Servidor.Servicios;

/// <summary>
/// Limitador de envíos por usuario para el hub de SignalR.
/// El middleware de limitación de ASP.NET Core solo cubre peticiones HTTP; las
/// invocaciones dentro de una conexión ya establecida necesitan este control propio.
/// </summary>
public sealed class LimitadorEnvioMensajes : IDisposable
{
    private readonly ConcurrentDictionary<Guid, RateLimiter> _limitadores = new();
    private readonly SignalROptions _opciones;
    private bool _liberado;

    /// <summary>Crea el limitador.</summary>
    /// <param name="opciones">Opciones de SignalR, que definen el máximo por minuto.</param>
    public LimitadorEnvioMensajes(IOptions<SignalROptions> opciones)
    {
        ArgumentNullException.ThrowIfNull(opciones);
        _opciones = opciones.Value;
    }

    /// <summary>Intenta consumir un permiso de envío para el usuario indicado.</summary>
    /// <param name="usuarioId">Usuario que envía.</param>
    /// <returns><c>true</c> si el envío está permitido.</returns>
    public bool IntentarConsumir(Guid usuarioId)
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
        return permiso.IsAcquired;
    }

    /// <summary>Libera el limitador asociado a un usuario que ya no tiene conexiones.</summary>
    /// <param name="usuarioId">Usuario a olvidar.</param>
    public void Olvidar(Guid usuarioId)
    {
        if (_limitadores.TryRemove(usuarioId, out var limitador))
        {
            limitador.Dispose();
        }
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
