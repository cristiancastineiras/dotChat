namespace Chat.Aplicacion.Dtos;

/// <summary>Proyección pública de un usuario. Nunca expone hashes ni sellos de seguridad.</summary>
/// <param name="Id">Identificador del usuario.</param>
/// <param name="NombreUsuario">Nombre de usuario.</param>
/// <param name="Email">Correo electrónico.</param>
/// <param name="FechaCreacion">Fecha UTC de alta.</param>
/// <param name="FechaUltimoAcceso">Fecha UTC del último inicio de sesión.</param>
/// <param name="Activo">Indica si la cuenta está habilitada.</param>
/// <param name="EnLinea">
/// Indica si el usuario tiene alguna conexión en tiempo real abierta. Es un dato
/// volátil que se resuelve fuera de la caché, ya en la respuesta.
/// </param>
/// <param name="TieneAvatar">Indica si el usuario tiene foto de perfil que descargar.</param>
/// <param name="AvatarActualizado">
/// Fecha del último cambio de foto. El cliente la usa como marca de versión: mientras
/// no cambie puede seguir mostrando la copia que ya tenga descargada.
/// </param>
public sealed record UsuarioDto(
    Guid Id,
    string NombreUsuario,
    string Email,
    DateTimeOffset FechaCreacion,
    DateTimeOffset? FechaUltimoAcceso,
    bool Activo,
    bool EnLinea = false,
    bool TieneAvatar = false,
    DateTimeOffset? AvatarActualizado = null);

/// <summary>
/// Identidad del usuario autenticado tal como la ve él mismo: incluye sus roles, que
/// no se publican en la proyección que ven los demás.
/// </summary>
/// <param name="Id">Identificador del usuario.</param>
/// <param name="NombreUsuario">Nombre de usuario.</param>
/// <param name="Email">Correo electrónico.</param>
/// <param name="FechaCreacion">Fecha UTC de alta.</param>
/// <param name="FechaUltimoAcceso">Fecha UTC del último inicio de sesión.</param>
/// <param name="EsAdministrador">Indica si la cuenta tiene rol de administrador.</param>
/// <param name="TieneAvatar">Indica si el usuario tiene foto de perfil.</param>
/// <param name="AvatarActualizado">Fecha del último cambio de foto.</param>
public sealed record PerfilDto(
    Guid Id,
    string NombreUsuario,
    string Email,
    DateTimeOffset FechaCreacion,
    DateTimeOffset? FechaUltimoAcceso,
    bool EsAdministrador,
    bool TieneAvatar,
    DateTimeOffset? AvatarActualizado);

/// <summary>Estado de conexión de un usuario.</summary>
/// <param name="UsuarioId">Identificador del usuario.</param>
/// <param name="NombreUsuario">Nombre del usuario.</param>
/// <param name="EnLinea">Indica si tiene al menos una conexión abierta.</param>
/// <param name="UltimaVez">
/// Momento en el que se le vio por última vez: la hora de conexión si sigue en línea,
/// o la de su última desconexión observada por este proceso.
/// </param>
/// <param name="Conexiones">Número de conexiones simultáneas abiertas.</param>
public sealed record PresenciaDto(
    Guid UsuarioId,
    string NombreUsuario,
    bool EnLinea,
    DateTimeOffset? UltimaVez,
    int Conexiones);
