using Chat.Aplicacion.Abstracciones;
using Chat.Aplicacion.Cqrs;
using Chat.Aplicacion.Dtos;
using Chat.Aplicacion.Validacion;
using Chat.Dominio.Abstracciones;
using Chat.Dominio.Excepciones;
using Microsoft.Extensions.Logging;

namespace Chat.Aplicacion.Comandos.Salas;

/// <summary>Elimina una sala junto con sus mensajes y membresías. Operación administrativa.</summary>
/// <param name="SalaId">Sala a eliminar.</param>
public sealed record ComandoEliminarSala(Guid SalaId) : IComando<ResultadoOperacionDto>;

/// <summary>Manejador de <see cref="ComandoEliminarSala"/>.</summary>
public sealed class ManejadorEliminarSala : IManejadorComando<ComandoEliminarSala, ResultadoOperacionDto>
{
    private readonly IRepositorioSalas _salas;
    private readonly IUnidadDeTrabajo _unidadDeTrabajo;
    private readonly IServicioCache _cache;
    private readonly ILogger<ManejadorEliminarSala> _registro;

    /// <summary>Crea el manejador.</summary>
    public ManejadorEliminarSala(
        IRepositorioSalas salas,
        IUnidadDeTrabajo unidadDeTrabajo,
        IServicioCache cache,
        ILogger<ManejadorEliminarSala> registro)
    {
        _salas = salas;
        _unidadDeTrabajo = unidadDeTrabajo;
        _cache = cache;
        _registro = registro;
    }

    /// <inheritdoc />
    public async Task<ResultadoOperacionDto> ManejarAsync(
        ComandoEliminarSala comando,
        CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(comando);

        var salaId = ValidadorEntrada.ValidarIdentificador(comando.SalaId, "salaId");

        var sala = await _salas.ObtenerPorIdAsync(salaId, cancelacion).ConfigureAwait(false)
            ?? throw ExcepcionNoEncontrado.Para("La sala", salaId);

        _salas.Eliminar(sala);
        await _unidadDeTrabajo.GuardarCambiosAsync(cancelacion).ConfigureAwait(false);
        await _cache.InvalidarPorEtiquetaAsync(ClavesCache.EtiquetaSalas, cancelacion).ConfigureAwait(false);

        _registro.LogWarning("Sala eliminada. SalaId={SalaId} Nombre={Nombre}", sala.Id, sala.Nombre);

        return new ResultadoOperacionDto(true, $"Sala '{sala.Nombre}' eliminada junto con su historial.");
    }
}
