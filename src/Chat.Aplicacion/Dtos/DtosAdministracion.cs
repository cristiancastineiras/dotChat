namespace Chat.Aplicacion.Dtos;

/// <summary>Resumen de actividad de la plataforma para la consola de administración.</summary>
/// <param name="TotalUsuarios">Usuarios registrados.</param>
/// <param name="TotalSalas">Salas existentes.</param>
/// <param name="TotalMensajes">Mensajes almacenados.</param>
/// <param name="ConexionesActivas">Conexiones SignalR abiertas en este momento.</param>
/// <param name="UsuariosConectados">Usuarios distintos con al menos una conexión activa.</param>
/// <param name="FechaConsulta">Fecha UTC de generación del informe.</param>
public sealed record EstadisticasDto(
    int TotalUsuarios,
    int TotalSalas,
    int TotalMensajes,
    int ConexionesActivas,
    int UsuariosConectados,
    DateTimeOffset FechaConsulta);

/// <summary>Conexión SignalR activa.</summary>
/// <param name="ConexionId">Identificador de conexión asignado por SignalR.</param>
/// <param name="UsuarioId">Usuario autenticado propietario de la conexión.</param>
/// <param name="NombreUsuario">Nombre del usuario.</param>
/// <param name="FechaConexion">Fecha UTC en la que se estableció la conexión.</param>
/// <param name="Salas">Nombres de las salas a las que está suscrita la conexión.</param>
public sealed record ConexionActivaDto(
    string ConexionId,
    Guid UsuarioId,
    string NombreUsuario,
    DateTimeOffset FechaConexion,
    IReadOnlyList<string> Salas);

/// <summary>Resultado de una operación administrativa sin datos de retorno.</summary>
/// <param name="Exito">Indica si la operación se completó.</param>
/// <param name="Mensaje">Descripción del resultado, apta para mostrar en consola.</param>
public sealed record ResultadoOperacionDto(bool Exito, string Mensaje);
