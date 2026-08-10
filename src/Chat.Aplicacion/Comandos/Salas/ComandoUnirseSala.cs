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

/// <summary>Da de alta a un usuario como miembro de una sala. Es idempotente.</summary>
/// <param name="SalaId">Sala destino.</param>
/// <param name="UsuarioId">Usuario que se une.</param>
public sealed record ComandoUnirseSala(Guid SalaId, Guid UsuarioId) : IComando<SalaDto>;

/// <summary>Manejador de <see cref="ComandoUnirseSala"/>.</summary>
public sealed class ManejadorUnirseSala : IManejadorComando<ComandoUnirseSala, SalaDto>
{
    private readonly IRepositorioSalas _salas;
    private readonly IUnidadDeTrabajo _unidadDeTrabajo;
    private readonly IServicioCache _cache;
    private readonly IProveedorFechaHora _reloj;
    private readonly ILogger<ManejadorUnirseSala> _registro;

    /// <summary>Crea el manejador.</summary>
    public ManejadorUnirseSala(
        IRepositorioSalas salas,
        IUnidadDeTrabajo unidadDeTrabajo,
        IServicioCache cache,
        IProveedorFechaHora reloj,
        ILogger<ManejadorUnirseSala> registro)
    {
        _salas = salas;
        _unidadDeTrabajo = unidadDeTrabajo;
        _cache = cache;
        _reloj = reloj;
        _registro = registro;
    }

    /// <inheritdoc />
    public async Task<SalaDto> ManejarAsync(ComandoUnirseSala comando, CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(comando);

        var salaId = ValidadorEntrada.ValidarIdentificador(comando.SalaId, "salaId");
        var usuarioId = ValidadorEntrada.ValidarIdentificador(comando.UsuarioId, "usuarioId");

        var sala = await _salas.ObtenerPorIdAsync(salaId, cancelacion).ConfigureAwait(false)
            ?? throw ExcepcionNoEncontrado.Para("La sala", salaId);

        var membresia = await _salas.ObtenerMembresiaAsync(salaId, usuarioId, cancelacion).ConfigureAwait(false);
        if (membresia is null)
        {
            // Solo las salas públicas admiten que alguien entre por su cuenta. A las
            // privadas se entra por invitación y las directas nacen con sus dos
            // miembros y no admiten un tercero.
            if (sala.Tipo != TipoSala.Publica)
            {
                throw new ExcepcionAutorizacion(
                    sala.Tipo == TipoSala.Directa
                        ? "No se puede entrar en una conversación directa ajena."
                        : "La sala es privada: alguno de sus miembros debe invitarte.");
            }

            await _salas.AgregarMembresiaAsync(
                new MiembroSala { SalaId = salaId, UsuarioId = usuarioId, FechaUnion = _reloj.Ahora },
                cancelacion).ConfigureAwait(false);

            await _unidadDeTrabajo.GuardarCambiosAsync(cancelacion).ConfigureAwait(false);
            await _cache.InvalidarPorEtiquetaAsync(ClavesCache.EtiquetaSalas, cancelacion).ConfigureAwait(false);

            _registro.LogInformation(
                "Usuario unido a sala. SalaId={SalaId} UsuarioId={UsuarioId}",
                salaId,
                usuarioId);
        }

        var miembros = await _salas.ListarMiembrosAsync(salaId, cancelacion).ConfigureAwait(false);
        return sala.ADto(miembros.Count, NombreVisible(sala, miembros, usuarioId), esMiembro: true);
    }

    /// <summary>
    /// Devuelve el nombre con el que se presenta la sala al usuario: el del
    /// interlocutor si es una conversación directa, y el propio en el resto.
    /// </summary>
    /// <param name="sala">Sala consultada.</param>
    /// <param name="miembros">Miembros de la sala, con su usuario cargado.</param>
    /// <param name="usuarioId">Usuario desde cuyo punto de vista se calcula.</param>
    private static string NombreVisible(Sala sala, IReadOnlyList<MiembroSala> miembros, Guid usuarioId)
    {
        if (sala.Tipo != TipoSala.Directa)
        {
            return sala.Nombre;
        }

        foreach (var miembro in miembros)
        {
            if (miembro.UsuarioId != usuarioId)
            {
                return miembro.Usuario?.UserName ?? Proyecciones.NombreDesconocido;
            }
        }

        return Proyecciones.NombreDesconocido;
    }
}
