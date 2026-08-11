using Chat.Aplicacion.Abstracciones;
using Chat.Aplicacion.Opciones;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Chat.Infraestructura.Presencia;

/// <summary>
/// Limitador de envíos compartido por todas las réplicas, con ventana fija por usuario.
/// </summary>
/// <remarks>
/// <para>
/// La cuenta vive en Valkey, de modo que el cupo es del usuario y no del nodo: abrir
/// una segunda conexión que caiga en otra réplica no da derecho a un segundo cupo.
/// </para>
/// <para>
/// La ventana es fija y no deslizante a propósito. Una deslizante exigiría guardar la
/// marca de cada envío y podarlas en cada comprobación; una fija se resuelve con un
/// contador que se autodestruye, que es una operación de coste constante y memoria
/// despreciable para un control cuyo objetivo es frenar inundaciones, no repartir cupo
/// con precisión de milisegundo.
/// </para>
/// </remarks>
public sealed class LimitadorEnviosValkey : ILimitadorEnvios
{
    /// <summary>
    /// Incrementa el contador de la ventana y le pone caducidad la primera vez.
    /// Devuelve el número de envíos que lleva el usuario en la ventana actual.
    /// </summary>
    private const string Guion =
        """
        local cuenta = redis.call('INCR', KEYS[1])

        if cuenta == 1 then
            redis.call('EXPIRE', KEYS[1], ARGV[1])
        end

        return cuenta
        """;

    /// <summary>Duración de la ventana.</summary>
    private static readonly TimeSpan Ventana = TimeSpan.FromMinutes(1);

    private readonly IConnectionMultiplexer _conexion;
    private readonly SignalROptions _signalR;
    private readonly ILogger<LimitadorEnviosValkey> _registro;
    private readonly string _prefijo;

    /// <summary>Crea el limitador.</summary>
    /// <param name="conexion">Conexión compartida con Valkey.</param>
    /// <param name="signalR">Opciones de SignalR, que definen el máximo por minuto.</param>
    /// <param name="valkey">Opciones de Valkey, de las que sale el prefijo de claves.</param>
    /// <param name="registro">Registro estructurado.</param>
    public LimitadorEnviosValkey(
        IConnectionMultiplexer conexion,
        IOptions<SignalROptions> signalR,
        IOptions<ValkeyOptions> valkey,
        ILogger<LimitadorEnviosValkey> registro)
    {
        ArgumentNullException.ThrowIfNull(signalR);
        ArgumentNullException.ThrowIfNull(valkey);

        _conexion = conexion;
        _signalR = signalR.Value;
        _registro = registro;
        _prefijo = valkey.Value.PrefijoClaves();
    }

    /// <inheritdoc />
    public async Task<bool> IntentarConsumirAsync(Guid usuarioId, CancellationToken cancelacion = default)
    {
        // La ventana va en la clave: al cambiar de minuto se estrena contador sin
        // tener que borrar el anterior, que caduca solo.
        var ventana = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / (long)Ventana.TotalSeconds;
        var clave = $"{_prefijo}limite:envios:{usuarioId:N}:{ventana}";

        try
        {
            var cuenta = (long)await _conexion.GetDatabase()
                .ScriptEvaluateAsync(
                    Guion,
                    [clave],
                    [(int)Ventana.TotalSeconds + 5])
                .ConfigureAwait(false);

            return cuenta <= _signalR.MaximoMensajesPorMinuto;
        }
        catch (RedisException excepcion)
        {
            // Si Valkey no responde, se deja pasar el mensaje. Un limitador caído no
            // debe convertirse en una denegación de servicio para los usuarios
            // legítimos; el resto de controles —autorización, tamaño, antirrepetición—
            // siguen en pie.
            _registro.LogWarning(
                excepcion,
                "No se pudo consultar el límite de envíos; se permite el mensaje. UsuarioId={UsuarioId}",
                usuarioId);

            return true;
        }
    }
}
