using Chat.Aplicacion.Dtos;

namespace Chat.Aplicacion.Abstracciones;

/// <summary>
/// Registro en memoria de las conexiones en tiempo real activas. Sostiene dos cosas:
/// la lista de conexiones que ve la consola de administración y la presencia
/// («en línea» / «desconectado») que ven los demás usuarios.
/// </summary>
public interface IRegistroConexiones
{
    /// <summary>Registra una conexión recién establecida.</summary>
    /// <param name="conexionId">Identificador asignado por SignalR.</param>
    /// <param name="usuarioId">Usuario autenticado.</param>
    /// <param name="nombreUsuario">Nombre del usuario.</param>
    /// <param name="fechaConexion">Fecha UTC de conexión.</param>
    /// <returns>
    /// <c>true</c> si es la primera conexión del usuario, es decir, si acaba de pasar
    /// a estar en línea y hay que anunciarlo a los demás.
    /// </returns>
    bool Registrar(string conexionId, Guid usuarioId, string nombreUsuario, DateTimeOffset fechaConexion);

    /// <summary>Elimina una conexión cerrada.</summary>
    /// <param name="conexionId">Identificador de conexión.</param>
    /// <param name="fechaDesconexion">Fecha UTC del cierre.</param>
    /// <returns>
    /// Datos de la conexión cerrada, o <c>null</c> si no constaba registrada
    /// (por ejemplo, si la conexión se rechazó antes de completarse).
    /// </returns>
    ConexionCerrada? Eliminar(string conexionId, DateTimeOffset fechaDesconexion);

    /// <summary>Asocia una sala a una conexión.</summary>
    /// <param name="conexionId">Identificador de conexión.</param>
    /// <param name="nombreSala">Nombre de la sala.</param>
    void AgregarSala(string conexionId, string nombreSala);

    /// <summary>Desasocia una sala de una conexión.</summary>
    /// <param name="conexionId">Identificador de conexión.</param>
    /// <param name="nombreSala">Nombre de la sala.</param>
    void QuitarSala(string conexionId, string nombreSala);

    /// <summary>Devuelve una instantánea de las conexiones activas.</summary>
    IReadOnlyList<ConexionActivaDto> Listar();

    /// <summary>Indica si un usuario tiene al menos una conexión abierta.</summary>
    /// <param name="usuarioId">Usuario consultado.</param>
    bool EstaConectado(Guid usuarioId);

    /// <summary>
    /// Devuelve los identificadores de conexión abiertos de un usuario. Sirve para
    /// suscribir sus clientes a una sala creada mientras ya estaba conectado.
    /// </summary>
    /// <param name="usuarioId">Usuario consultado.</param>
    IReadOnlyList<string> ConexionesDe(Guid usuarioId);

    /// <summary>
    /// Devuelve la presencia de todos los usuarios conocidos por este proceso:
    /// los conectados ahora y los que lo estuvieron desde el arranque.
    /// </summary>
    IReadOnlyList<PresenciaDto> ListarPresencia();

    /// <summary>Número de conexiones abiertas.</summary>
    int TotalConexiones { get; }

    /// <summary>Número de usuarios distintos con al menos una conexión abierta.</summary>
    int TotalUsuariosConectados { get; }
}

/// <summary>Datos de una conexión que acaba de cerrarse.</summary>
/// <param name="UsuarioId">Usuario propietario de la conexión.</param>
/// <param name="NombreUsuario">Nombre del usuario.</param>
/// <param name="FueLaUltima">
/// Indica si era la única conexión que le quedaba al usuario, es decir, si acaba
/// de pasar a estar desconectado.
/// </param>
public sealed record ConexionCerrada(Guid UsuarioId, string NombreUsuario, bool FueLaUltima);
