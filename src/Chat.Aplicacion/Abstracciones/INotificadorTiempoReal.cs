using Chat.Aplicacion.Dtos;

namespace Chat.Aplicacion.Abstracciones;

/// <summary>
/// Difunde eventos a los clientes conectados. La capa de aplicación depende de esta
/// abstracción y no del <c>IHubContext</c> de SignalR.
/// </summary>
public interface INotificadorTiempoReal
{
    /// <summary>Envía un mensaje nuevo a todos los miembros conectados de una sala.</summary>
    /// <param name="mensaje">Mensaje ya descifrado.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    Task NotificarMensajeAsync(MensajeDto mensaje, CancellationToken cancelacion = default);

    /// <summary>Avisa a la sala de que un usuario se ha unido.</summary>
    /// <param name="nombreSala">Nombre de la sala.</param>
    /// <param name="nombreUsuario">Nombre del usuario.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    Task NotificarUsuarioUnidoAsync(string nombreSala, string nombreUsuario, CancellationToken cancelacion = default);

    /// <summary>Avisa a la sala de que un usuario la ha abandonado.</summary>
    /// <param name="nombreSala">Nombre de la sala.</param>
    /// <param name="nombreUsuario">Nombre del usuario.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    Task NotificarUsuarioSalidoAsync(string nombreSala, string nombreUsuario, CancellationToken cancelacion = default);

    /// <summary>Difunde a todos los clientes que una sala pública ha sido creada.</summary>
    /// <param name="sala">Sala creada.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    Task NotificarSalaCreadaAsync(SalaDto sala, CancellationToken cancelacion = default);

    /// <summary>
    /// Avisa a un usuario concreto de que tiene una sala nueva a su disposición
    /// (una conversación directa recién abierta o una invitación a una sala privada)
    /// y suscribe sus conexiones abiertas para que reciba los mensajes al instante.
    /// </summary>
    /// <param name="usuarioId">Destinatario del aviso.</param>
    /// <param name="sala">Sala a la que ya pertenece.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    Task NotificarSalaDisponibleAsync(Guid usuarioId, SalaDto sala, CancellationToken cancelacion = default);

    /// <summary>Difunde el cambio de estado de conexión de un usuario.</summary>
    /// <param name="presencia">Estado resultante.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    Task NotificarPresenciaAsync(PresenciaDto presencia, CancellationToken cancelacion = default);
}
