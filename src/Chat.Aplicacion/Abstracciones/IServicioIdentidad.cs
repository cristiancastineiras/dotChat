using Chat.Dominio.Entidades;

namespace Chat.Aplicacion.Abstracciones;

/// <summary>Resultado de una operación de Identity que puede acumular varios errores.</summary>
/// <param name="Exito">Indica si la operación se completó.</param>
/// <param name="Errores">Mensajes de error devueltos por Identity.</param>
public sealed record ResultadoIdentidad(bool Exito, IReadOnlyList<string> Errores)
{
    /// <summary>Resultado correcto sin errores.</summary>
    public static ResultadoIdentidad Correcto { get; } = new(true, []);

    /// <summary>Crea un resultado fallido a partir de los errores indicados.</summary>
    /// <param name="errores">Mensajes de error.</param>
    public static ResultadoIdentidad Fallido(params string[] errores) => new(false, errores);
}

/// <summary>
/// Abstrae ASP.NET Core Identity (<c>UserManager</c> / <c>RoleManager</c>) para que la capa
/// de aplicación pueda gestionar cuentas sin acoplarse a la infraestructura.
/// </summary>
public interface IServicioIdentidad
{
    /// <summary>Crea un usuario con contraseña, aplicando las políticas de Identity.</summary>
    /// <param name="usuario">Usuario a crear.</param>
    /// <param name="clave">Contraseña en claro (Identity almacena solo el hash).</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    Task<ResultadoIdentidad> CrearUsuarioAsync(Usuario usuario, string clave, CancellationToken cancelacion = default);

    /// <summary>Asigna un rol a un usuario, creándolo si no existe.</summary>
    /// <param name="usuario">Usuario destino.</param>
    /// <param name="rol">Nombre del rol.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    Task<ResultadoIdentidad> AsignarRolAsync(Usuario usuario, string rol, CancellationToken cancelacion = default);

    /// <summary>
    /// Verifica las credenciales de un usuario aplicando el bloqueo por intentos fallidos.
    /// </summary>
    /// <param name="usuario">Usuario a verificar.</param>
    /// <param name="clave">Contraseña en claro.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    /// <returns><c>true</c> si la contraseña es correcta y la cuenta no está bloqueada.</returns>
    Task<bool> VerificarClaveAsync(Usuario usuario, string clave, CancellationToken cancelacion = default);

    /// <summary>Obtiene los roles asignados a un usuario.</summary>
    /// <param name="usuario">Usuario consultado.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    Task<IReadOnlyList<string>> ObtenerRolesAsync(Usuario usuario, CancellationToken cancelacion = default);

    /// <summary>Indica si ya existe una cuenta con ese correo electrónico.</summary>
    /// <param name="email">Correo electrónico normalizado.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    Task<bool> ExisteEmailAsync(string email, CancellationToken cancelacion = default);

    /// <summary>Elimina definitivamente un usuario.</summary>
    /// <param name="usuario">Usuario a eliminar.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    Task<ResultadoIdentidad> EliminarUsuarioAsync(Usuario usuario, CancellationToken cancelacion = default);
}
