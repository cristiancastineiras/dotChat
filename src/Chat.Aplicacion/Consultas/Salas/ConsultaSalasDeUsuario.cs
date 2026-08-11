using Chat.Aplicacion.Abstracciones;
using Chat.Aplicacion.Cqrs;
using Chat.Aplicacion.Dtos;
using Chat.Aplicacion.Mapeos;
using Chat.Aplicacion.Validacion;
using Chat.Dominio.Abstracciones;
using Chat.Dominio.Entidades;

namespace Chat.Aplicacion.Consultas.Salas;

/// <summary>
/// Lista las salas y conversaciones de un usuario: su bandeja de entrada. Incluye
/// los mensajes pendientes de cada una, una previsualización del último mensaje y la
/// presencia del interlocutor en las conversaciones directas.
/// </summary>
/// <param name="UsuarioId">Usuario consultado.</param>
public sealed record ConsultaSalasDeUsuario(Guid UsuarioId) : IConsulta<IReadOnlyList<SalaDto>>;

/// <summary>
/// Manejador de <see cref="ConsultaSalasDeUsuario"/>. No se cachea: el resultado
/// depende del usuario y cambia con cada mensaje que recibe.
/// </summary>
/// <remarks>
/// Es la consulta que sostiene la pantalla principal del cliente, así que se resuelve
/// en cuatro viajes fijos —salas, pendientes, últimos mensajes y presencia— y no en un
/// número que crezca con las conversaciones que tenga el usuario.
/// </remarks>
public sealed class ManejadorSalasDeUsuario
    : IManejadorConsulta<ConsultaSalasDeUsuario, IReadOnlyList<SalaDto>>
{
    /// <summary>Longitud a la que se recorta la previsualización del último mensaje.</summary>
    private const int LongitudPrevisualizacion = 120;

    private readonly IRepositorioSalas _salas;
    private readonly IRepositorioMensajes _mensajes;
    private readonly ICifradorMensajes _cifrador;
    private readonly IRegistroConexiones _conexiones;

    /// <summary>Crea el manejador.</summary>
    public ManejadorSalasDeUsuario(
        IRepositorioSalas salas,
        IRepositorioMensajes mensajes,
        ICifradorMensajes cifrador,
        IRegistroConexiones conexiones)
    {
        _salas = salas;
        _mensajes = mensajes;
        _cifrador = cifrador;
        _conexiones = conexiones;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SalaDto>> ManejarAsync(
        ConsultaSalasDeUsuario consulta,
        CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(consulta);

        var usuarioId = ValidadorEntrada.ValidarIdentificador(consulta.UsuarioId, "usuarioId");

        var salas = await _salas.ListarDeUsuarioAsync(usuarioId, cancelacion).ConfigureAwait(false);

        if (salas.Count == 0)
        {
            return [];
        }

        var identificadores = salas.Select(sala => sala.Id).ToArray();

        var pendientes = await _mensajes
            .ContarNoLeidosPorSalaAsync(usuarioId, cancelacion)
            .ConfigureAwait(false);

        var ultimos = await _mensajes
            .ObtenerUltimosPorSalaAsync(identificadores, cancelacion)
            .ConfigureAwait(false);

        var conectados = await _conexiones
            .FiltrarConectadosAsync(InterlocutoresDe(salas, usuarioId), cancelacion)
            .ConfigureAwait(false);

        var resultado = new List<SalaDto> (salas.Count);

        foreach (var sala in salas)
        {
            var interlocutor = sala.Tipo == TipoSala.Directa ? InterlocutorDe(sala, usuarioId) : null;

            resultado.Add(sala.ADto(
                sala.Miembros.Count,
                sala.NombreVisiblePara(usuarioId),
                esMiembro: true,
                pendientes.TryGetValue(sala.Id, out var sinLeer) ? sinLeer : 0,
                ultimos.TryGetValue(sala.Id, out var ultimo) ? Resumir(ultimo, usuarioId) : null,
                interlocutor is null ? null : conectados.Contains(interlocutor.Value)));
        }

        return resultado;
    }

    /// <summary>Descifra y recorta el último mensaje para la previsualización.</summary>
    /// <param name="ultimo">Último mensaje leído de la base de datos.</param>
    /// <param name="usuarioId">Usuario que consulta.</param>
    private ResumenMensajeDto Resumir(UltimoMensajeSala ultimo, Guid usuarioId)
    {
        var texto = string.Empty;

        if (ultimo.TextoCifrado is not null
            && _cifrador.IntentarDescifrar(ultimo.TextoCifrado, out var descifrado)
            && descifrado is not null)
        {
            // La previsualización se recorta aquí y no en el cliente: no tiene sentido
            // mandar por la red dos mil caracteres para enseñar ciento veinte.
            texto = descifrado.Length > LongitudPrevisualizacion
                ? string.Concat(descifrado.AsSpan(0, LongitudPrevisualizacion), "…")
                : descifrado;
        }

        return new ResumenMensajeDto(
            ultimo.NombreAutor ?? Proyecciones.NombreDesconocido,
            ultimo.AutorId == usuarioId,
            texto,
            ultimo.FechaEnvio,
            ultimo.NombreAdjunto,
            ultimo.TipoAdjunto);
    }

    /// <summary>Reúne los interlocutores de todas las conversaciones directas.</summary>
    /// <param name="salas">Salas del usuario, con sus miembros cargados.</param>
    /// <param name="usuarioId">Usuario que consulta.</param>
    private static Guid[] InterlocutoresDe(IReadOnlyList<Sala> salas, Guid usuarioId)
        => [.. salas
            .Where(sala => sala.Tipo == TipoSala.Directa)
            .Select(sala => InterlocutorDe(sala, usuarioId))
            .Where(interlocutor => interlocutor is not null)
            .Select(interlocutor => interlocutor!.Value)];

    /// <summary>Localiza al otro participante de una conversación directa.</summary>
    /// <param name="sala">Conversación directa, con sus miembros cargados.</param>
    /// <param name="usuarioId">Usuario que consulta.</param>
    private static Guid? InterlocutorDe(Sala sala, Guid usuarioId)
    {
        foreach (var miembro in sala.Miembros)
        {
            if (miembro.UsuarioId != usuarioId)
            {
                return miembro.UsuarioId;
            }
        }

        return null;
    }
}
