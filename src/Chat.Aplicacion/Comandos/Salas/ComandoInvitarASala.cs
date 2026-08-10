using Chat.Aplicacion.Abstracciones;
using Chat.Aplicacion.Cqrs;
using Chat.Aplicacion.Dtos;
using Chat.Aplicacion.Mapeos;
using Chat.Aplicacion.Validacion;
using Chat.Dominio.Abstracciones;
using Chat.Dominio.Entidades;
using Chat.Dominio.Excepciones;
using Microsoft.Extensions.Logging;

namespace Chat.Aplicacion.Comandos.Salas;

/// <summary>
/// Incorpora a otra persona a una sala. Es la única vía de entrada a una sala
/// privada, y en las públicas equivale a un atajo cómodo.
/// </summary>
/// <param name="SalaId">Sala destino.</param>
/// <param name="AnfitrionId">Miembro que cursa la invitación.</param>
/// <param name="Solicitud">Nombre del usuario invitado.</param>
public sealed record ComandoInvitarASala(Guid SalaId, Guid AnfitrionId, SolicitudInvitarDto Solicitud)
    : IComando<ResultadoOperacionDto>;

/// <summary>Manejador de <see cref="ComandoInvitarASala"/>.</summary>
public sealed class ManejadorInvitarASala : IManejadorComando<ComandoInvitarASala, ResultadoOperacionDto>
{
    private readonly IRepositorioSalas _salas;
    private readonly IRepositorioUsuarios _usuarios;
    private readonly IUnidadDeTrabajo _unidadDeTrabajo;
    private readonly IServicioCache _cache;
    private readonly INotificadorTiempoReal _notificador;
    private readonly IProveedorFechaHora _reloj;
    private readonly ILogger<ManejadorInvitarASala> _registro;

    /// <summary>Crea el manejador.</summary>
    public ManejadorInvitarASala(
        IRepositorioSalas salas,
        IRepositorioUsuarios usuarios,
        IUnidadDeTrabajo unidadDeTrabajo,
        IServicioCache cache,
        INotificadorTiempoReal notificador,
        IProveedorFechaHora reloj,
        ILogger<ManejadorInvitarASala> registro)
    {
        _salas = salas;
        _usuarios = usuarios;
        _unidadDeTrabajo = unidadDeTrabajo;
        _cache = cache;
        _notificador = notificador;
        _reloj = reloj;
        _registro = registro;
    }

    /// <inheritdoc />
    public async Task<ResultadoOperacionDto> ManejarAsync(
        ComandoInvitarASala comando,
        CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(comando);

        var salaId = ValidadorEntrada.ValidarIdentificador(comando.SalaId, "salaId");
        var anfitrionId = ValidadorEntrada.ValidarIdentificador(comando.AnfitrionId, "anfitrionId");
        var nombre = ValidadorEntrada.ValidarNombreUsuario(comando.Solicitud.NombreUsuario);

        var sala = await _salas.ObtenerPorIdAsync(salaId, cancelacion).ConfigureAwait(false)
            ?? throw ExcepcionNoEncontrado.Para("La sala", salaId);

        if (sala.Tipo == TipoSala.Directa)
        {
            throw new ExcepcionAutorizacion(
                "Una conversación directa es cosa de dos: no admite invitados. Cree una sala privada.");
        }

        if (!await _salas.EsMiembroAsync(salaId, anfitrionId, cancelacion).ConfigureAwait(false))
        {
            throw new ExcepcionAutorizacion("Solo un miembro de la sala puede invitar a alguien.");
        }

        var invitado = await _usuarios.ObtenerPorNombreAsync(nombre, cancelacion).ConfigureAwait(false)
            ?? throw new ExcepcionNoEncontrado($"No existe ningún usuario llamado '{nombre}'.");

        if (!invitado.Activo)
        {
            throw new ExcepcionConflicto($"La cuenta '{invitado.UserName}' está desactivada.");
        }

        if (await _salas.ObtenerMembresiaAsync(salaId, invitado.Id, cancelacion).ConfigureAwait(false) is not null)
        {
            return new ResultadoOperacionDto(true, $"'{invitado.UserName}' ya pertenecía a la sala.");
        }

        await _salas.AgregarMembresiaAsync(
            new MiembroSala { SalaId = salaId, UsuarioId = invitado.Id, FechaUnion = _reloj.Ahora },
            cancelacion).ConfigureAwait(false);

        await _unidadDeTrabajo.GuardarCambiosAsync(cancelacion).ConfigureAwait(false);
        await _cache.InvalidarPorEtiquetaAsync(ClavesCache.EtiquetaSalas, cancelacion).ConfigureAwait(false);

        var totalMiembros = await _salas.ContarMiembrosAsync(salaId, cancelacion).ConfigureAwait(false);

        await _notificador.NotificarSalaDisponibleAsync(
            invitado.Id,
            sala.ADto(totalMiembros, esMiembro: true),
            cancelacion).ConfigureAwait(false);

        await _notificador.NotificarUsuarioUnidoAsync(
            sala.Nombre,
            invitado.UserName ?? Proyecciones.NombreDesconocido,
            cancelacion).ConfigureAwait(false);

        _registro.LogInformation(
            "Invitación cursada. SalaId={SalaId} AnfitrionId={AnfitrionId} InvitadoId={InvitadoId}",
            salaId,
            anfitrionId,
            invitado.Id);

        return new ResultadoOperacionDto(true, $"'{invitado.UserName}' ya forma parte de la sala '{sala.Nombre}'.");
    }
}
