using Chat.Aplicacion.Abstracciones;
using Chat.Aplicacion.Opciones;
using Chat.Dominio.Abstracciones;
using Microsoft.Extensions.Options;

namespace Chat.Servidor.Servicios;

/// <summary>
/// Tarea en segundo plano que purga periódicamente lo que ya no sirve: los tokens de
/// refresco caducados o revocados y las imágenes que se subieron pero nunca llegaron a
/// publicarse, para que ni una tabla ni la otra crezcan de forma indefinida.
/// </summary>
public sealed class ServicioMantenimiento : BackgroundService
{
    /// <summary>Periodo entre ejecuciones de la purga.</summary>
    private static readonly TimeSpan Periodo = TimeSpan.FromHours(6);

    /// <summary>
    /// Desfase aleatorio máximo de la primera pasada. Con varias réplicas levantadas a
    /// la vez, sin él todas purgarían en el mismo instante y competirían por las mismas
    /// filas; repartirlas en el tiempo hace que casi siempre trabaje una sola.
    /// </summary>
    private static readonly TimeSpan DesfaseMaximoInicial = TimeSpan.FromMinutes(5);

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
        await Task.Delay(Random.Shared.Next((int)DesfaseMaximoInicial.TotalMilliseconds), cancelacion)
            .ConfigureAwait(false);

        using var temporizador = new PeriodicTimer(Periodo);

        do
        {
            try
            {
                await PurgarAsync(cancelacion).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancelacion.IsCancellationRequested)
            {
                // Apagado ordenado del servidor: no es un error.
                return;
            }
            catch (Exception excepcion)
            {
                // Un fallo puntual no debe detener la tarea periódica.
                _registro.LogError(excepcion, "Error durante el mantenimiento periódico.");
            }
        }
        while (await temporizador.WaitForNextTickAsync(cancelacion).ConfigureAwait(false));
    }

    /// <summary>Elimina los tokens inservibles y los adjuntos abandonados.</summary>
    /// <param name="cancelacion">Token de cancelación.</param>
    private async Task PurgarAsync(CancellationToken cancelacion)
    {
        await using var ambito = _fabricaAmbitos.CreateAsyncScope();

        var tokens = ambito.ServiceProvider.GetRequiredService<IRepositorioTokensRefresco>();
        var adjuntos = ambito.ServiceProvider.GetRequiredService<IRepositorioAdjuntos>();
        var almacen = ambito.ServiceProvider.GetRequiredService<IAlmacenObjetos>();
        var reloj = ambito.ServiceProvider.GetRequiredService<IProveedorFechaHora>();
        var opciones = ambito.ServiceProvider.GetRequiredService<IOptions<AdjuntosOptions>>().Value;

        var ahora = reloj.Ahora;

        var tokensEliminados = await tokens.PurgarAsync(ahora, cancelacion).ConfigureAwait(false);

        if (tokensEliminados > 0)
        {
            _registro.LogInformation("Purga de tokens completada. Eliminados={Eliminados}", tokensEliminados);
        }

        // Un adjunto sin mensaje es uno que se subió y nunca llegó a publicarse. El
        // margen es holgado a propósito: entre subir y publicar median segundos, pero
        // un cliente puede quedarse a medias y reintentar más tarde.
        var limite = ahora - TimeSpan.FromHours(opciones.HorasMargenHuerfanos);
        var claves = await adjuntos.PurgarHuerfanosAsync(limite, cancelacion).ConfigureAwait(false);

        if (claves.Count == 0)
        {
            return;
        }

        // Las fichas ya no están; ahora se retira el contenido. Si esto falla, quedan
        // objetos sin ficha: ocupan sitio, pero no rompen ninguna descarga.
        await almacen.EliminarVariosAsync(claves, cancelacion).ConfigureAwait(false);

        _registro.LogInformation(
            "Purga de adjuntos huérfanos completada. Eliminados={Eliminados}",
            claves.Count);
    }
}
