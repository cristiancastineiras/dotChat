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
/// Abre la conversación privada entre quien lo pide y otra persona. Es idempotente:
/// si la conversación ya existe se devuelve la misma, nunca se duplica.
/// </summary>
/// <param name="SolicitanteId">Usuario que abre la conversación.</param>
/// <param name="Solicitud">Interlocutor, indicado por nombre o por identificador.</param>
public sealed record ComandoAbrirConversacionDirecta(
    Guid SolicitanteId,
    SolicitudConversacionDirectaDto Solicitud) : IComando<SalaDto>;

/// <summary>Manejador de <see cref="ComandoAbrirConversacionDirecta"/>.</summary>
public sealed class ManejadorAbrirConversacionDirecta
    : IManejadorComando<ComandoAbrirConversacionDirecta, SalaDto>
{
    /// <summary>Miembros de una conversación directa: siempre dos.</summary>
    private const int MiembrosDeUnaDirecta = 2;

    private readonly IRepositorioSalas _salas;
    private readonly IRepositorioUsuarios _usuarios;
    private readonly IUnidadDeTrabajo _unidadDeTrabajo;
    private readonly IServicioCache _cache;
    private readonly INotificadorTiempoReal _notificador;
    private readonly IProveedorFechaHora _reloj;
    private readonly ILogger<ManejadorAbrirConversacionDirecta> _registro;

    /// <summary>Crea el manejador.</summary>
    public ManejadorAbrirConversacionDirecta(
        IRepositorioSalas salas,
        IRepositorioUsuarios usuarios,
        IUnidadDeTrabajo unidadDeTrabajo,
        IServicioCache cache,
        INotificadorTiempoReal notificador,
        IProveedorFechaHora reloj,
        ILogger<ManejadorAbrirConversacionDirecta> registro)
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
    public async Task<SalaDto> ManejarAsync(
        ComandoAbrirConversacionDirecta comando,
        CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(comando);

        var solicitanteId = ValidadorEntrada.ValidarIdentificador(comando.SolicitanteId, "solicitanteId");

        var solicitante = await _usuarios.ObtenerPorIdAsync(solicitanteId, cancelacion).ConfigureAwait(false)
            ?? throw ExcepcionNoEncontrado.Para("El usuario", solicitanteId);

        var destinatario = await ResolverDestinatarioAsync(comando.Solicitud, cancelacion).ConfigureAwait(false);

        if (destinatario.Id == solicitanteId)
        {
            throw new ExcepcionValidacion("usuario", "No puedes abrir una conversación contigo mismo.");
        }

        if (!destinatario.Activo)
        {
            throw new ExcepcionConflicto(
                $"La cuenta '{destinatario.UserName}' está desactivada y no admite mensajes.");
        }

        var clave = Sala.ConstruirClaveDirecta(solicitanteId, destinatario.Id);
        var existente = await _salas.ObtenerPorClaveDirectaAsync(clave, cancelacion).ConfigureAwait(false);

        if (existente is not null)
        {
            return existente.ADto(
                MiembrosDeUnaDirecta,
                destinatario.UserName,
                esMiembro: true);
        }

        var sala = await CrearAsync(solicitanteId, destinatario.Id, clave, cancelacion).ConfigureAwait(false);

        // El interlocutor recibe la conversación al instante: si está conectado, sus
        // clientes se suscriben al grupo sin tener que reiniciar la sesión.
        await _notificador.NotificarSalaDisponibleAsync(
            destinatario.Id,
            sala.ADto(MiembrosDeUnaDirecta, solicitante.UserName, esMiembro: true),
            cancelacion).ConfigureAwait(false);

        _registro.LogInformation(
            "Conversación directa abierta. SalaId={SalaId} SolicitanteId={SolicitanteId} DestinatarioId={DestinatarioId}",
            sala.Id,
            solicitanteId,
            destinatario.Id);

        return sala.ADto(MiembrosDeUnaDirecta, destinatario.UserName, esMiembro: true);
    }

    /// <summary>Localiza al interlocutor por identificador o por nombre de usuario.</summary>
    /// <param name="solicitud">Datos recibidos del cliente.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    private async Task<Usuario> ResolverDestinatarioAsync(
        SolicitudConversacionDirectaDto solicitud,
        CancellationToken cancelacion)
    {
        if (solicitud.UsuarioId is { } identificador && identificador != Guid.Empty)
        {
            return await _usuarios.ObtenerPorIdAsync(identificador, cancelacion).ConfigureAwait(false)
                ?? throw ExcepcionNoEncontrado.Para("El usuario", identificador);
        }

        if (string.IsNullOrWhiteSpace(solicitud.NombreUsuario))
        {
            throw new ExcepcionValidacion(
                "usuario",
                "Indique con quién quiere hablar, por nombre de usuario o por identificador.");
        }

        var nombre = ValidadorEntrada.ValidarNombreUsuario(solicitud.NombreUsuario);

        return await _usuarios.ObtenerPorNombreAsync(nombre, cancelacion).ConfigureAwait(false)
            ?? throw new ExcepcionNoEncontrado($"No existe ningún usuario llamado '{nombre}'.");
    }

    /// <summary>Crea la sala directa con sus dos membresías y la persiste.</summary>
    /// <param name="solicitanteId">Usuario que abre la conversación.</param>
    /// <param name="destinatarioId">Interlocutor.</param>
    /// <param name="clave">Clave canónica de la pareja.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    private async Task<Sala> CrearAsync(
        Guid solicitanteId,
        Guid destinatarioId,
        string clave,
        CancellationToken cancelacion)
    {
        var ahora = _reloj.Ahora;

        var sala = new Sala
        {
            Id = Guid.CreateVersion7(),
            Nombre = Sala.ConstruirNombreDirecto(clave),
            Descripcion = null,
            Tipo = TipoSala.Directa,
            ClaveDirecta = clave,
            FechaCreacion = ahora,
            CreadorId = solicitanteId
        };

        await _salas.AgregarAsync(sala, cancelacion).ConfigureAwait(false);

        await _salas.AgregarMembresiaAsync(
            new MiembroSala
            {
                SalaId = sala.Id,
                UsuarioId = solicitanteId,
                FechaUnion = ahora,
                FechaUltimaLectura = ahora
            },
            cancelacion).ConfigureAwait(false);

        await _salas.AgregarMembresiaAsync(
            new MiembroSala { SalaId = sala.Id, UsuarioId = destinatarioId, FechaUnion = ahora },
            cancelacion).ConfigureAwait(false);

        await _unidadDeTrabajo.GuardarCambiosAsync(cancelacion).ConfigureAwait(false);
        await _cache.InvalidarPorEtiquetaAsync(ClavesCache.EtiquetaSalas, cancelacion).ConfigureAwait(false);

        return sala;
    }
}
