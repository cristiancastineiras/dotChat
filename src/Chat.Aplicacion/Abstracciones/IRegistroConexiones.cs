using Chat.Aplicacion.Dtos;

namespace Chat.Aplicacion.Abstracciones;

/// <summary>
/// Registro de las conexiones en tiempo real activas. Sostiene dos cosas: la lista de
/// conexiones que ve la consola de administración y la presencia («en línea» /
/// «desconectado») que ven los demás usuarios.
/// </summary>
/// <remarks>
/// <para>
/// El estado es del <b>clúster entero</b>, no del proceso. Con varias réplicas detrás
/// de un balanceador, dos conexiones del mismo usuario pueden caer en réplicas
/// distintas, y si cada una llevara su propia cuenta, cerrar una anunciaría que el
/// usuario se ha desconectado mientras sigue escribiendo desde la otra.
/// </para>
/// <para>
/// Todas las operaciones son asíncronas justamente por eso: detrás hay una llamada de
/// red a un almacén compartido, no un diccionario en memoria.
/// </para>
/// </remarks>
public interface IRegistroConexiones
{
    /// <summary>Registra una conexión recién establecida.</summary>
    /// <param name="conexionId">Identificador asignado por SignalR.</param>
    /// <param name="usuarioId">Usuario autenticado.</param>
    /// <param name="nombreUsuario">Nombre del usuario.</param>
    /// <param name="fechaConexion">Fecha UTC de conexión.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    /// <returns>
    /// <c>true</c> si es la primera conexión del usuario en todo el clúster, es decir,
    /// si acaba de pasar a estar en línea y hay que anunciarlo a los demás.
    /// </returns>
    Task<bool> RegistrarAsync(
        string conexionId,
        Guid usuarioId,
        string nombreUsuario,
        DateTimeOffset fechaConexion,
        CancellationToken cancelacion = default);

    /// <summary>Elimina una conexión cerrada.</summary>
    /// <param name="conexionId">Identificador de conexión.</param>
    /// <param name="fechaDesconexion">Fecha UTC del cierre.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    /// <returns>
    /// Datos de la conexión cerrada, o <c>null</c> si no constaba registrada
    /// (por ejemplo, si la conexión se rechazó antes de completarse).
    /// </returns>
    Task<ConexionCerrada?> EliminarAsync(
        string conexionId,
        DateTimeOffset fechaDesconexion,
        CancellationToken cancelacion = default);

    /// <summary>Asocia una sala a una conexión.</summary>
    /// <param name="conexionId">Identificador de conexión.</param>
    /// <param name="nombreSala">Nombre de la sala.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    Task AgregarSalaAsync(string conexionId, string nombreSala, CancellationToken cancelacion = default);

    /// <summary>Desasocia una sala de una conexión.</summary>
    /// <param name="conexionId">Identificador de conexión.</param>
    /// <param name="nombreSala">Nombre de la sala.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    Task QuitarSalaAsync(string conexionId, string nombreSala, CancellationToken cancelacion = default);

    /// <summary>Devuelve una instantánea de las conexiones activas de todo el clúster.</summary>
    /// <param name="cancelacion">Token de cancelación.</param>
    Task<IReadOnlyList<ConexionActivaDto>> ListarAsync(CancellationToken cancelacion = default);

    /// <summary>Indica si un usuario tiene al menos una conexión abierta en algún nodo.</summary>
    /// <param name="usuarioId">Usuario consultado.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    Task<bool> EstaConectadoAsync(Guid usuarioId, CancellationToken cancelacion = default);

    /// <summary>Filtra, de un conjunto de usuarios, los que están conectados.</summary>
    /// <remarks>
    /// Existe para que pintar la lista de miembros de una sala no cueste una llamada
    /// de red por miembro.
    /// </remarks>
    /// <param name="usuarioIds">Usuarios a comprobar.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    Task<IReadOnlySet<Guid>> FiltrarConectadosAsync(
        IReadOnlyCollection<Guid> usuarioIds,
        CancellationToken cancelacion = default);

    /// <summary>
    /// Devuelve los identificadores de conexión abiertos de un usuario. Sirve para
    /// suscribir sus clientes a una sala creada mientras ya estaba conectado.
    /// </summary>
    /// <param name="usuarioId">Usuario consultado.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    Task<IReadOnlyList<string>> ConexionesDeAsync(Guid usuarioId, CancellationToken cancelacion = default);

    /// <summary>Devuelve la presencia de todos los usuarios conocidos por el clúster.</summary>
    /// <param name="cancelacion">Token de cancelación.</param>
    Task<IReadOnlyList<PresenciaDto>> ListarPresenciaAsync(CancellationToken cancelacion = default);

    /// <summary>Número de conexiones abiertas en todo el clúster.</summary>
    /// <param name="cancelacion">Token de cancelación.</param>
    Task<int> ContarConexionesAsync(CancellationToken cancelacion = default);

    /// <summary>Número de usuarios distintos con al menos una conexión abierta.</summary>
    /// <param name="cancelacion">Token de cancelación.</param>
    Task<int> ContarUsuariosConectadosAsync(CancellationToken cancelacion = default);

    /// <summary>
    /// Anuncia que esta réplica sigue viva y retira las conexiones de las que hayan
    /// dejado de anunciarse.
    /// </summary>
    /// <remarks>
    /// Es la red de seguridad frente a una réplica que se cae de golpe: sus conexiones
    /// nunca reciben el cierre ordenado y, sin esta limpieza, sus usuarios se quedarían
    /// «en línea» para siempre.
    /// </remarks>
    /// <param name="margenSinSenal">Tiempo sin dar señales tras el cual una réplica se da por muerta.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    /// <returns>Presencias que han pasado a desconectadas como consecuencia de la limpieza.</returns>
    Task<IReadOnlyList<PresenciaDto>> LatirYLimpiarAsync(
        TimeSpan margenSinSenal,
        CancellationToken cancelacion = default);
}

/// <summary>Datos de una conexión que acaba de cerrarse.</summary>
/// <param name="UsuarioId">Usuario propietario de la conexión.</param>
/// <param name="NombreUsuario">Nombre del usuario.</param>
/// <param name="FueLaUltima">
/// Indica si era la única conexión que le quedaba al usuario en todo el clúster, es
/// decir, si acaba de pasar a estar desconectado.
/// </param>
public sealed record ConexionCerrada(Guid UsuarioId, string NombreUsuario, bool FueLaUltima);
