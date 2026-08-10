using System.ComponentModel.DataAnnotations;

namespace Chat.Aplicacion.Opciones;

/// <summary>
/// Configuración de emisión y validación de tokens JWT.
/// La clave de firma nunca se versiona: se inyecta mediante «user secrets» o variables de entorno.
/// </summary>
public sealed class JwtOptions
{
    /// <summary>Nombre de la sección en appsettings.json.</summary>
    public const string Seccion = "Jwt";

    /// <summary>Emisor esperado de los tokens (claim <c>iss</c>).</summary>
    [Required(ErrorMessage = "El emisor (Issuer) del JWT es obligatorio.")]
    public string Emisor { get; set; } = string.Empty;

    /// <summary>Audiencia esperada de los tokens (claim <c>aud</c>).</summary>
    [Required(ErrorMessage = "La audiencia (Audience) del JWT es obligatoria.")]
    public string Audiencia { get; set; } = string.Empty;

    /// <summary>
    /// Clave simétrica de firma en Base64. Debe tener al menos 32 bytes (256 bits)
    /// para ser compatible con HMAC-SHA256 sin degradar la seguridad.
    /// </summary>
    [Required(ErrorMessage = "La clave de firma del JWT es obligatoria y debe configurarse fuera del código.")]
    [MinLength(44, ErrorMessage = "La clave de firma debe representar al menos 32 bytes codificados en Base64.")]
    public string ClaveFirmaBase64 { get; set; } = string.Empty;

    /// <summary>Minutos de validez del token de acceso.</summary>
    [Range(1, 1440, ErrorMessage = "La vigencia del token de acceso debe estar entre 1 y 1440 minutos.")]
    public int MinutosVigenciaAcceso { get; set; } = 30;

    /// <summary>Días de validez del token de refresco.</summary>
    [Range(1, 365, ErrorMessage = "La vigencia del token de refresco debe estar entre 1 y 365 días.")]
    public int DiasVigenciaRefresco { get; set; } = 7;

    /// <summary>Margen de tolerancia de reloj, en segundos, al validar la expiración.</summary>
    [Range(0, 300, ErrorMessage = "La tolerancia de reloj debe estar entre 0 y 300 segundos.")]
    public int SegundosToleranciaReloj { get; set; } = 30;
}
