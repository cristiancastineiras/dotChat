using Chat.Aplicacion.Abstracciones;
using Chat.Dominio.Abstracciones;

namespace Chat.Servidor.Servicios;

/// <summary>
/// Tarea en segundo plano que purga periódicamente los tokens de refresco
/// caducados o revocados, para que la tabla no crezca de forma indefinida.
/// </summary>
public sealed class ServicioMantenimiento : BackgroundService
{
    /// <summary>Periodo entre ejecuciones de la purga.</summary>
    private static readonly TimeSpan Periodo = TimeSpan.FromHours(6);

    private readonly IServiceScopeFactory _fabricaAmbitos;
    private readonly ILogger<ServicioMantenimiento> _registro;

    /// <summary>Crea el servicio de mantenimiento.</summary>
    /// <param name="fabricaAmbitos">Fábrica de ámbitos de dependencias.</param>
    /// <param name="registro">Registro estructurado.</param>
    public ServicioMantenimiento(IServiceScopeFactory fabricaAmbitos, ILogger<ServicioMantenimiento> registro)
    {
        _fabricaAmbitos = fabricaAmbitos;
        _registro = registro;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken cancelacion)
    {
        using var temporizador = new PeriodicTimer(Periodo);

        do
        {
            try
            {
                await PurgarTokensAsync(cancelacion).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancelacion.IsCancellationRequested)
            {
                // Apagado ordenado del servidor: no es un error.
                return;
            }
            catch (Exception excepcion)
            {
                // Un fallo puntual no debe detener la tarea periódica.
                _registro.LogError(excepcion, "Error durante la purga de tokens de refresco.");
            }
        }
        while (await temporizador.WaitForNextTickAsync(cancelacion).ConfigureAwait(false));
    }

    /// <summary>Elimina los tokens de refresco ya inservibles.</summary>
    /// <param name="cancelacion">Token de cancelación.</param>
    private async Task PurgarTokensAsync(CancellationToken cancelacion)
    {
        await using var ambito = _fabricaAmbitos.CreateAsyncScope();

        var tokens = ambito.ServiceProvider.GetRequiredService<IRepositorioTokensRefresco>();
        var reloj = ambito.ServiceProvider.GetRequiredService<IProveedorFechaHora>();

        var eliminados = await tokens.PurgarAsync(reloj.Ahora, cancelacion).ConfigureAwait(false);

        if (eliminados > 0)
        {
            _registro.LogInformation("Purga de tokens completada. Eliminados={Eliminados}", eliminados);
        }
    }
}
