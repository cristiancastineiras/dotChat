using Chat.Aplicacion.Dtos;

namespace Chat.Servidor.Hubs;

/// <summary>
/// Métodos que el servidor puede invocar en el cliente. Al declararlos en una
/// interfaz, SignalR genera un contrato fuertemente tipado y se evitan cadenas
/// mágicas tanto en el servidor como en el cliente CLI.
/// </summary>
public interface IClienteChat
{
    /// <summary>Entrega un mensaje nuevo de una sala a la que el cliente está suscrito.</summary>
    /// <param name="mensaje">Mensaje ya descifrado.</param>
    Task RecibirMensaje(MensajeDto mensaje);

    /// <summary>Avisa de que un usuario se ha unido a una sala.</summary>
    /// <param name="nombreSala">Nombre de la sala.</param>
    /// <param name="nombreUsuario">Nombre del usuario.</param>
    Task UsuarioUnido(string nombreSala, string nombreUsuario);

    /// <summary>Avisa de que un usuario ha abandonado una sala.</summary>
    /// <param name="nombreSala">Nombre de la sala.</param>
    /// <param name="nombreUsuario">Nombre del usuario.</param>
    Task UsuarioSalido(string nombreSala, string nombreUsuario);

    /// <summary>Avisa de que se ha creado una sala pública nueva.</summary>
    /// <param name="sala">Sala creada.</param>
    Task SalaCreada(SalaDto sala);

    /// <summary>
    /// Avisa a un usuario concreto de que tiene una sala nueva a su disposición:
    /// alguien le ha abierto una conversación directa o le ha invitado a una privada.
    /// </summary>
    /// <param name="sala">Sala, ya con el nombre visible desde su punto de vista.</param>
    Task SalaDisponible(SalaDto sala);

    /// <summary>Comunica que un usuario ha pasado a estar en línea o desconectado.</summary>
    /// <param name="presencia">Estado resultante.</param>
    Task PresenciaCambiada(PresenciaDto presencia);

    /// <summary>Indica que alguien está escribiendo en una sala.</summary>
    /// <param name="salaId">Sala en la que se escribe.</param>
    /// <param name="nombreUsuario">Nombre de quien escribe.</param>
    Task UsuarioEscribiendo(Guid salaId, string nombreUsuario);

    /// <summary>Comunica al cliente el resultado de su conexión al hub.</summary>
    /// <param name="nombreUsuario">Nombre del usuario autenticado.</param>
    /// <param name="salas">Salas y conversaciones a las que ya pertenece.</param>
    Task Conectado(string nombreUsuario, IReadOnlyList<SalaDto> salas);

    /// <summary>Notifica un error recuperable producido al procesar una invocación.</summary>
    /// <param name="mensaje">Descripción apta para mostrar al usuario.</param>
    Task ErrorRecibido(string mensaje);
}
