using Chat.Aplicacion.Abstracciones;
using Chat.Aplicacion.Cqrs;
using Chat.Aplicacion.Dtos;
using Chat.Aplicacion.Validacion;
using Chat.Dominio.Abstracciones;
using Chat.Dominio.Excepciones;

namespace Chat.Aplicacion.Comandos.Salas;

/// <summary>
/// Adelanta la marca de lectura de un usuario en una sala hasta el momento actual,
/// dejando su contador de mensajes pendientes a cero.
/// </summary>
/// <param name="SalaId">Sala leída.</param>
/// <param name="UsuarioId">Usuario que la ha leído.</param>
public sealed record ComandoMarcarSalaLeida(Guid SalaId, Guid UsuarioId) : IComando<ResultadoOperacionDto>;

/// <summary>Manejador de <see cref="ComandoMarcarSalaLeida"/>.</summary>
public sealed class ManejadorMarcarSalaLeida : IManejadorComando<ComandoMarcarSalaLeida, ResultadoOperacionDto>
{
    private readonly IRepositorioSalas _salas;
    private readonly IUnidadDeTrabajo _unidadDeTrabajo;
    private readonly IProveedorFechaHora _reloj;

    /// <summary>Crea el manejador.</summary>
    public ManejadorMarcarSalaLeida(
        IRepositorioSalas salas,
        IUnidadDeTrabajo unidadDeTrabajo,
        IProveedorFechaHora reloj)
    {
        _salas = salas;
        _unidadDeTrabajo = unidadDeTrabajo;
        _reloj = reloj;
    }

    /// <inheritdoc />
    public async Task<ResultadoOperacionDto> ManejarAsync(
        ComandoMarcarSalaLeida comando,
        CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(comando);

        var salaId = ValidadorEntrada.ValidarIdentificador(comando.SalaId, "salaId");
        var usuarioId = ValidadorEntrada.ValidarIdentificador(comando.UsuarioId, "usuarioId");

        var membresia = await _salas.ObtenerMembresiaAsync(salaId, usuarioId, cancelacion).ConfigureAwait(false)
            ?? throw new ExcepcionAutorizacion("Solo los miembros de la sala pueden marcarla como leída.");

        membresia.FechaUltimaLectura = _reloj.Ahora;
        await _unidadDeTrabajo.GuardarCambiosAsync(cancelacion).ConfigureAwait(false);

        // No se invalida la caché de salas: la marca de lectura es un dato por
        // usuario que se resuelve fuera del catálogo cacheado.
        return new ResultadoOperacionDto(true, "Conversación marcada como leída.");
    }
}
