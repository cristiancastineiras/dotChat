using Chat.Aplicacion.Abstracciones;

namespace Chat.Servidor.Servicios;

/// <summary>
/// Anuncia periódicamente que esta réplica sigue viva y retira lo que dejan atrás las
/// que se caen.
/// </summary>
/// <remarks>
/// <para>
/// Cuando una réplica se apaga de forma ordenada, cada conexión pasa por el cierre del
/// hub y se limpia sola. Cuando se cae de golpe —un contenedor que muere, un nodo que
/// se reinicia— sus conexiones no reciben nada y se quedan registradas: sus usuarios
/// aparecerían «en línea» indefinidamente y el recuento de conexiones no bajaría nunca.
/// </para>
/// <para>
/// Cada réplica marca su hora en el almacén compartido. La que ejecuta la limpieza
/// retira las conexiones de las que llevan demasiado tiempo sin marcar y anuncia las
/// desconexiones resultantes, para que los clientes actualicen la presencia igual que
/// si el cierre hubiera sido ordenado.
/// </para>
/// </remarks>
public sealed class ServicioLatidoReplica : BackgroundService
{
    /// <summary>Cada cuánto anuncia la réplica que sigue viva.</summary>
    private static readonly TimeSpan Intervalo = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Tiempo sin señales tras el cual una réplica se da por muerta. Es varias veces el
    /// intervalo: una pausa larga del recolector o un pico de carga no deben provocar
    /// que se declare muerta a una réplica que está perfectamente viva.
    /// </summary>
    private static readonly TimeSpan MargenSinSenal = TimeSpan.FromSeconds(45);

    private readonly IServiceScopeFactory _fabricaAmbitos;
    private readonly ILogger<ServicioLatidoReplica> _registro;

    /// <summary>Crea el servicio de latido.</summary>
    /// <param name="fabricaAmbitos">Fábrica de ámbitos de dependencias.</param>
    /// <param name="registro">Registro estructurado.</param>
    public ServicioLatidoReplica(IServiceScopeFactory fabricaAmbitos, ILogger<ServicioLatidoReplica> registro)
    {
        _fabricaAmbitos = fabricaAmbitos;
        _registro = registro;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken cancelacion)
    {
        using var temporizador = new PeriodicTimer(Intervalo);

        do
        {
            try
            {
                await LatirAsync(cancelacion).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancelacion.IsCancellationRequested)
            {
                return;
            }
            catch (Exception excepcion)
            {
                // Perder un latido no es grave: el margen tolera varios seguidos.
                _registro.LogWarning(excepcion, "No se pudo anunciar el latido de la réplica.");
            }
        }
        while (await temporizador.WaitForNextTickAsync(cancelacion).ConfigureAwait(false));
    }

    /// <summary>Marca la hora de esta réplica y difunde las desconexiones que se descubran.</summary>
    /// <param name="cancelacion">Token de cancelación.</param>
    private async Task LatirAsync(CancellationToken cancelacion)
    {
        await using var ambito = _fabricaAmbitos.CreateAsyncScope();

        var conexiones = ambito.ServiceProvider.GetRequiredService<IRegistroConexiones>();
        var notificador = ambito.ServiceProvider.GetRequiredService<INotificadorTiempoReal>();

        var desconectados = await conexiones
            .LatirYLimpiarAsync(MargenSinSenal, cancelacion)
            .ConfigureAwait(false);

        foreach (var presencia in desconectados)
        {
            _registro.LogInformation(
                "Presencia liberada tras la caída de una réplica. UsuarioId={UsuarioId} NombreUsuario={NombreUsuario}",
                presencia.UsuarioId,
                presencia.NombreUsuario);

            await notificador.NotificarPresenciaAsync(presencia, cancelacion).ConfigureAwait(false);
        }
    }
}
