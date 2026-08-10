using System.ComponentModel.DataAnnotations;

namespace Chat.AdminCli.Servicios;

/// <summary>Configuración de la consola de administración.</summary>
public sealed class OpcionesAdmin
{
    /// <summary>Nombre de la sección en appsettings.json.</summary>
    public const string Seccion = "Admin";

    /// <summary>Dirección base del servidor de mensajería.</summary>
    [Required(ErrorMessage = "La dirección del servidor es obligatoria.")]
    public string UrlServidor { get; set; } = "https://localhost:7150";

    /// <summary>Segundos de espera máximos para una llamada HTTP.</summary>
    [Range(1, 300, ErrorMessage = "El tiempo de espera debe estar entre 1 y 300 segundos.")]
    public int SegundosTiempoEspera { get; set; } = 30;

    /// <summary>
    /// Nombre del administrador. Si se deja vacío, la consola lo pide de forma interactiva.
    /// </summary>
    public string NombreUsuario { get; set; } = string.Empty;

    /// <summary>
    /// Contraseña del administrador. Nunca debe escribirse en appsettings.json:
    /// se define mediante la variable de entorno <c>DOTCHAT_Admin__Clave</c> o se pide por consola.
    /// </summary>
    public string Clave { get; set; } = string.Empty;

    /// <summary>
    /// Acepta certificados TLS no verificables. Solo para desarrollo local contra
    /// un certificado autofirmado que no esté en el almacén de confianza.
    /// </summary>
    public bool AceptarCertificadosNoConfiables { get; set; }
}
