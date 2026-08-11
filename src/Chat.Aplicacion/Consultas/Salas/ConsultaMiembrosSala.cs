using Chat.Aplicacion.Abstracciones;
using Chat.Aplicacion.Cqrs;
using Chat.Aplicacion.Dtos;
using Chat.Aplicacion.Mapeos;
using Chat.Aplicacion.Validacion;
using Chat.Dominio.Abstracciones;
using Chat.Dominio.Excepciones;
using ZLinq;

namespace Chat.Aplicacion.Consultas.Salas;

/// <summary>Lista los miembros de una sala con su estado de conexión.</summary>
/// <param name="SalaId">Sala consultada.</param>
/// <param name="SolicitanteId">Usuario que consulta; debe ser miembro salvo que sea administrador.</param>
/// <param name="OmitirComprobacionMembresia">Reservado a administradores.</param>
public sealed record ConsultaMiembrosSala(
    Guid SalaId,
    Guid SolicitanteId,
    bool OmitirComprobacionMembresia = false) : IConsulta<IReadOnlyList<MiembroSalaDto>>;

/// <summary>
/// Manejador de <see cref="ConsultaMiembrosSala"/>. No se cachea porque la mitad del
/// dato —quién está en línea— es volátil por definición.
/// </summary>
public sealed class ManejadorMiembrosSala
    : IManejadorConsulta<ConsultaMiembrosSala, IReadOnlyList<MiembroSalaDto>>
{
    private readonly IRepositorioSalas _salas;
    private readonly IRegistroConexiones _conexiones;

    /// <summary>Crea el manejador.</summary>
    public ManejadorMiembrosSala(IRepositorioSalas salas, IRegistroConexiones conexiones)
    {
        _salas = salas;
        _conexiones = conexiones;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<MiembroSalaDto>> ManejarAsync(
        ConsultaMiembrosSala consulta,
        CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(consulta);

        var salaId = ValidadorEntrada.ValidarIdentificador(consulta.SalaId, "salaId");

        var sala = await _salas.ObtenerPorIdAsync(salaId, cancelacion).ConfigureAwait(false)
            ?? throw ExcepcionNoEncontrado.Para("La sala", salaId);

        if (!consulta.OmitirComprobacionMembresia
            && !await _salas.EsMiembroAsync(salaId, consulta.SolicitanteId, cancelacion).ConfigureAwait(false))
        {
            throw new ExcepcionAutorizacion("Solo los miembros de la sala pueden ver quién la compone.");
        }

        var miembros = await _salas.ListarMiembrosAsync(salaId, cancelacion).ConfigureAwait(false);

        // La presencia de todos los miembros se resuelve de una vez: con el registro
        // compartido, preguntar uno a uno sería una ida y vuelta por miembro.
        var conectados = await _conexiones
            .FiltrarConectadosAsync([.. miembros.Select(miembro => miembro.UsuarioId)], cancelacion)
            .ConfigureAwait(false);

        return miembros
            .AsValueEnumerable()
            .Select(miembro => miembro.ADto(
                conectados.Contains(miembro.UsuarioId),
                sala.CreadorId == miembro.UsuarioId))
            .ToArray();
    }
}
