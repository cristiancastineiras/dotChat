using Chat.Aplicacion.Abstracciones;
using Chat.Aplicacion.Cqrs;
using Chat.Aplicacion.Dtos;
using Chat.Aplicacion.Validacion;
using Chat.Dominio.Abstracciones;
using Chat.Dominio.Excepciones;
using Microsoft.Extensions.Logging;

namespace Chat.Aplicacion.Comandos.Salas;

/// <summary>Da de baja a un usuario de una sala. Es idempotente.</summary>
/// <param name="SalaId">Sala de origen.</param>
/// <param name="UsuarioId">Usuario que la abandona.</param>
public sealed record ComandoSalirSala(Guid SalaId, Guid UsuarioId) : IComando<ResultadoOperacionDto>;

/// <summary>Manejador de <see cref="ComandoSalirSala"/>.</summary>
public sealed class ManejadorSalirSala : IManejadorComando<ComandoSalirSala, ResultadoOperacionDto>
{
    private readonly IRepositorioSalas _salas;
    private readonly IUnidadDeTrabajo _unidadDeTrabajo;
    private readonly IServicioCache _cache;
    private readonly ILogger<ManejadorSalirSala> _registro;

    /// <summary>Crea el manejador.</summary>
    public ManejadorSalirSala(
        IRepositorioSalas salas,
        IUnidadDeTrabajo unidadDeTrabajo,
        IServicioCache cache,
        ILogger<ManejadorSalirSala> registro)
    {
        _salas = salas;
        _unidadDeTrabajo = unidadDeTrabajo;
        _cache = cache;
        _registro = registro;
    }

    /// <inheritdoc />
    public async Task<ResultadoOperacionDto> ManejarAsync(
        ComandoSalirSala comando,
        CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(comando);

        var salaId = ValidadorEntrada.ValidarIdentificador(comando.SalaId, "salaId");
        var usuarioId = ValidadorEntrada.ValidarIdentificador(comando.UsuarioId, "usuarioId");

        var sala = await _salas.ObtenerPorIdAsync(salaId, cancelacion).ConfigureAwait(false)
            ?? throw ExcepcionNoEncontrado.Para("La sala", salaId);

        var membresia = await _salas.ObtenerMembresiaAsync(salaId, usuarioId, cancelacion).ConfigureAwait(false);
        if (membresia is null)
        {
            return new ResultadoOperacionDto(true, $"No pertenecías a la sala '{sala.Nombre}'.");
        }

        _salas.EliminarMembresia(membresia);
        await _unidadDeTrabajo.GuardarCambiosAsync(cancelacion).ConfigureAwait(false);
        await _cache.InvalidarPorEtiquetaAsync(ClavesCache.EtiquetaSalas, cancelacion).ConfigureAwait(false);

        _registro.LogInformation(
            "Usuario salió de la sala. SalaId={SalaId} UsuarioId={UsuarioId}",
            salaId,
            usuarioId);

        return new ResultadoOperacionDto(true, $"Has salido de la sala '{sala.Nombre}'.");
    }
}
