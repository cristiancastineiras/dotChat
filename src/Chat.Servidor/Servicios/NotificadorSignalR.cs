using Chat.Aplicacion.Abstracciones;
using Chat.Aplicacion.Dtos;
using Chat.Servidor.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace Chat.Servidor.Servicios;

/// <summary>
/// Implementación de <see cref="INotificadorTiempoReal"/> sobre el hub de SignalR.
/// Vive en la capa de presentación: es la única que conoce <c>IHubContext</c>.
/// </summary>
public sealed class NotificadorSignalR : INotificadorTiempoReal
{
    private readonly IHubContext<ChatHub, IClienteChat> _hub;
    private readonly IRegistroConexiones _conexiones;

    /// <summary>Crea el notificador.</summary>
    /// <param name="hub">Contexto del hub de chat.</param>
    /// <param name="conexiones">Registro de conexiones, usado para suscribir a grupos nuevos.</param>
    public NotificadorSignalR(IHubContext<ChatHub, IClienteChat> hub, IRegistroConexiones conexiones)
    {
        _hub = hub;
        _conexiones = conexiones;
    }

    /// <inheritdoc />
    public Task NotificarMensajeAsync(MensajeDto mensaje, CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(mensaje);
        return _hub.Clients.Group(ChatHub.NombreGrupo(mensaje.SalaId)).RecibirMensaje(mensaje);
    }

    /// <inheritdoc />
    public Task NotificarUsuarioUnidoAsync(
        string nombreSala,
        string nombreUsuario,
        CancellationToken cancelacion = default)
        => _hub.Clients.All.UsuarioUnido(nombreSala, nombreUsuario);

    /// <inheritdoc />
    public Task NotificarUsuarioSalidoAsync(
        string nombreSala,
        string nombreUsuario,
        CancellationToken cancelacion = default)
        => _hub.Clients.All.UsuarioSalido(nombreSala, nombreUsuario);

    /// <inheritdoc />
    public Task NotificarSalaCreadaAsync(SalaDto sala, CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(sala);
        return _hub.Clients.All.SalaCreada(sala);
    }

    /// <inheritdoc />
    public async Task NotificarSalaDisponibleAsync(
        Guid usuarioId,
        SalaDto sala,
        CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(sala);

        // Sin esto, quien ya estaba conectado no recibiría los mensajes de la sala
        // nueva hasta reconectarse: su conexión se suscribió a los grupos que existían
        // en el momento de entrar.
        var grupo = ChatHub.NombreGrupo(sala.Id);

        foreach (var conexionId in _conexiones.ConexionesDe(usuarioId))
        {
            await _hub.Groups.AddToGroupAsync(conexionId, grupo, cancelacion).ConfigureAwait(false);
        }

        await _hub.Clients.User(usuarioId.ToString()).SalaDisponible(sala).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task NotificarPresenciaAsync(PresenciaDto presencia, CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(presencia);
        return _hub.Clients.All.PresenciaCambiada(presencia);
    }
}
