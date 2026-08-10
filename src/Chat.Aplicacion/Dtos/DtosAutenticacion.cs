namespace Chat.Aplicacion.Dtos;

/// <summary>Datos de entrada para registrar una cuenta nueva.</summary>
/// <param name="NombreUsuario">Nombre de usuario único.</param>
/// <param name="Email">Correo electrónico único.</param>
/// <param name="Clave">Contraseña en claro (nunca se almacena; Identity guarda solo su hash).</param>
public sealed record SolicitudRegistroDto(string NombreUsuario, string Email, string Clave);

/// <summary>Datos de entrada para iniciar sesión.</summary>
/// <param name="NombreUsuario">Nombre de usuario.</param>
/// <param name="Clave">Contraseña en claro.</param>
public sealed record SolicitudLoginDto(string NombreUsuario, string Clave);

/// <summary>Datos de entrada para renovar la sesión con un token de refresco.</summary>
/// <param name="TokenRefresco">Token de refresco entregado en el último inicio de sesión.</param>
public sealed record SolicitudRefrescoDto(string TokenRefresco);

/// <summary>Resultado de una autenticación correcta.</summary>
/// <param name="UsuarioId">Identificador del usuario autenticado.</param>
/// <param name="NombreUsuario">Nombre de usuario.</param>
/// <param name="TokenAcceso">Token JWT de acceso.</param>
/// <param name="ExpiraEn">Fecha UTC de expiración del token de acceso.</param>
/// <param name="TokenRefresco">Token de refresco de un solo uso.</param>
/// <param name="Roles">Roles asignados al usuario.</param>
public sealed record RespuestaAutenticacionDto(
    Guid UsuarioId,
    string NombreUsuario,
    string TokenAcceso,
    DateTimeOffset ExpiraEn,
    string TokenRefresco,
    IReadOnlyList<string> Roles);
