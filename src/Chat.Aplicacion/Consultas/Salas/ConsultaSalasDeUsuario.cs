using Chat.Aplicacion.Cqrs;
using Chat.Aplicacion.Dtos;
using Chat.Aplicacion.Mapeos;
using Chat.Aplicacion.Validacion;
using Chat.Dominio.Abstracciones;
using ZLinq;

namespace Chat.Aplicacion.Consultas.Salas;

/// <summary>
/// Lista las salas y conversaciones de un usuario: su bandeja de entrada. Incluye
/// los mensajes pendientes de cada una y ordena por actividad reciente.
/// </summary>
/// <param name="UsuarioId">Usuario consultado.</param>
public sealed record ConsultaSalasDeUsuario(Guid UsuarioId) : IConsulta<IReadOnlyList<SalaDto>>;

/// <summary>
/// Manejador de <see cref="ConsultaSalasDeUsuario"/>. No se cachea: el resultado
/// depende del usuario y cambia con cada mensaje que recibe.
/// </summary>
public sealed class ManejadorSalasDeUsuario
    : IManejadorConsulta<ConsultaSalasDeUsuario, IReadOnlyList<SalaDto>>
{
    private readonly IRepositorioSalas _salas;
    private readonly IRepositorioMensajes _mensajes;

    /// <summary>Crea el manejador.</summary>
    public ManejadorSalasDeUsuario(IRepositorioSalas salas, IRepositorioMensajes mensajes)
    {
        _salas = salas;
        _mensajes = mensajes;
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

        var pendientes = await _mensajes
            .ContarNoLeidosPorSalaAsync(usuarioId, cancelacion)
            .ConfigureAwait(false);

        return salas
            .AsValueEnumerable()
            .Select(sala => sala.ADto(
                sala.Miembros.Count,
                sala.NombreVisiblePara(usuarioId),
                esMiembro: true,
                pendientes.TryGetValue(sala.Id, out var sinLeer) ? sinLeer : 0))
            .ToArray();
    }
}
