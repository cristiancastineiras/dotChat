using System.ComponentModel.DataAnnotations;

namespace Chat.Aplicacion.Opciones;

/// <summary>
/// Configuración del cifrado de mensajes en reposo (AES-256-GCM).
/// La clave nunca se escribe en el código fuente ni en appsettings.json versionado.
/// </summary>
public sealed class CifradoOptions
{
    /// <summary>Nombre de la sección en appsettings.json.</summary>
    public const string Seccion = "Cifrado";

    /// <summary>Clave AES de 256 bits codificada en Base64 (32 bytes → 44 caracteres).</summary>
    [Required(ErrorMessage = "La clave de cifrado es obligatoria y debe configurarse fuera del código.")]
    [MinLength(44, ErrorMessage = "La clave de cifrado debe ser de 32 bytes codificados en Base64.")]
    public string ClaveBase64 { get; set; } = string.Empty;

    /// <summary>
    /// Etiqueta de contexto asociada al cifrado (AAD, «datos autenticados adicionales»).
    /// Vincula el texto cifrado a esta aplicación e impide reutilizarlo en otro contexto.
    /// </summary>
    [Required(ErrorMessage = "El contexto asociado del cifrado es obligatorio.")]
    public string ContextoAsociado { get; set; } = "dotchat:mensaje:v1";

    /// <summary>Longitud máxima permitida para el texto en claro de un mensaje.</summary>
    [Range(1, 8192, ErrorMessage = "La longitud máxima de mensaje debe estar entre 1 y 8192 caracteres.")]
    public int LongitudMaximaMensaje { get; set; } = 2000;
}
