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

/// <summary>Crea una sala nueva y da de alta al creador como primer miembro.</summary>
/// <param name="Solicitud">Datos de la sala.</param>
/// <param name="CreadorId">Usuario que la crea.</param>
public sealed record ComandoCrearSala(SolicitudCrearSalaDto Solicitud, Guid CreadorId) : IComando<SalaDto>;

/// <summary>Manejador de <see cref="ComandoCrearSala"/>.</summary>
public sealed class ManejadorCrearSala : IManejadorComando<ComandoCrearSala, SalaDto>
{
    private readonly IRepositorioSalas _salas;
    private readonly IUnidadDeTrabajo _unidadDeTrabajo;
    private readonly IServicioCache _cache;
    private readonly INotificadorTiempoReal _notificador;
    private readonly IProveedorFechaHora _reloj;
    private readonly ILogger<ManejadorCrearSala> _registro;

    /// <summary>Crea el manejador.</summary>
    public ManejadorCrearSala(
        IRepositorioSalas salas,
        IUnidadDeTrabajo unidadDeTrabajo,
        IServicioCache cache,
        INotificadorTiempoReal notificador,
        IProveedorFechaHora reloj,
        ILogger<ManejadorCrearSala> registro)
    {
        _salas = salas;
        _unidadDeTrabajo = unidadDeTrabajo;
        _cache = cache;
        _notificador = notificador;
        _reloj = reloj;
        _registro = registro;
    }

    /// <inheritdoc />
    public async Task<SalaDto> ManejarAsync(ComandoCrearSala comando, CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(comando);

        var nombre = ValidadorEntrada.ValidarNombreSala(comando.Solicitud.Nombre);
        var descripcion = ValidadorEntrada.ValidarDescripcionSala(comando.Solicitud.Descripcion);
        var creadorId = ValidadorEntrada.ValidarIdentificador(comando.CreadorId, "creadorId");

        if (await _salas.ObtenerPorNombreAsync(nombre, cancelacion).ConfigureAwait(false) is not null)
        {
            throw new ExcepcionConflicto($"Ya existe una sala llamada '{nombre}'.");
        }

        var ahora = _reloj.Ahora;
        var sala = new Sala
        {
            Id = Guid.CreateVersion7(),
            Nombre = nombre,
            Descripcion = descripcion,
            Tipo = comando.Solicitud.Privada ? TipoSala.Privada : TipoSala.Publica,
            FechaCreacion = ahora,
            CreadorId = creadorId
        };

        await _salas.AgregarAsync(sala, cancelacion).ConfigureAwait(false);
        await _salas.AgregarMembresiaAsync(
            new MiembroSala
            {
                SalaId = sala.Id,
                UsuarioId = creadorId,
                FechaUnion = ahora,
                FechaUltimaLectura = ahora
            },
            cancelacion).ConfigureAwait(false);

        await _unidadDeTrabajo.GuardarCambiosAsync(cancelacion).ConfigureAwait(false);
        await _cache.InvalidarPorEtiquetaAsync(ClavesCache.EtiquetaSalas, cancelacion).ConfigureAwait(false);

        var dto = sala.ADto(totalMiembros: 1, esMiembro: true);

        // Una sala privada no se anuncia: solo existe para quienes son invitados.
        if (sala.Tipo == TipoSala.Publica)
        {
            await _notificador.NotificarSalaCreadaAsync(dto, cancelacion).ConfigureAwait(false);
        }

        _registro.LogInformation(
            "Sala creada. SalaId={SalaId} Nombre={Nombre} Tipo={Tipo} CreadorId={CreadorId}",
            sala.Id,
            sala.Nombre,
            sala.Tipo,
            creadorId);

        return dto;
    }
}
