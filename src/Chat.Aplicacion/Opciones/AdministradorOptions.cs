using System.ComponentModel.DataAnnotations;

namespace Chat.Aplicacion.Opciones;

/// <summary>
/// Cuenta administrativa que se crea la primera vez que arranca el servidor.
/// La contraseña procede siempre de «user secrets» o de una variable de entorno.
/// </summary>
public sealed class AdministradorOptions
{
    /// <summary>Nombre de la sección en appsettings.json.</summary>
    public const string Seccion = "Administrador";

    /// <summary>Nombre de usuario del administrador inicial.</summary>
    [Required(ErrorMessage = "El nombre del administrador inicial es obligatorio.")]
    public string NombreUsuario { get; set; } = "admin";

    /// <summary>Correo electrónico del administrador inicial.</summary>
    [Required(ErrorMessage = "El correo del administrador inicial es obligatorio.")]
    [EmailAddress(ErrorMessage = "El correo del administrador inicial no es válido.")]
    public string Email { get; set; } = "admin@dotchat.local";

    /// <summary>
    /// Contraseña inicial. Si se deja vacía, el servidor no crea la cuenta y avisa
    /// por registro, en lugar de asignar una contraseña conocida.
    /// </summary>
    public string Clave { get; set; } = string.Empty;

    /// <summary>Nombre de la sala que se crea de serie para que la plataforma sea usable al instante.</summary>
    [Required(ErrorMessage = "El nombre de la sala inicial es obligatorio.")]
    public string SalaInicial { get; set; } = "General";
}
