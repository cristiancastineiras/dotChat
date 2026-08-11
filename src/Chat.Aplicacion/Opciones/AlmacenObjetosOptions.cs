using System.ComponentModel.DataAnnotations;

namespace Chat.Aplicacion.Opciones;

/// <summary>
/// Configuración del almacén de objetos compatible con S3 donde viven los archivos
/// adjuntos. En desarrollo apunta a MinIO.
/// </summary>
public sealed class AlmacenObjetosOptions
{
    /// <summary>Nombre de la sección en appsettings.json.</summary>
    public const string Seccion = "AlmacenObjetos";

    /// <summary>Dirección del servicio, incluido el esquema y el puerto.</summary>
    [Required(ErrorMessage = "La dirección del almacén de objetos es obligatoria.")]
    public string PuntoEntrada { get; set; } = "http://localhost:9000";

    /// <summary>Identificador de acceso.</summary>
    [Required(ErrorMessage = "La clave de acceso al almacén de objetos es obligatoria.")]
    public string ClaveAcceso { get; set; } = string.Empty;

    /// <summary>Clave secreta.</summary>
    [Required(ErrorMessage = "La clave secreta del almacén de objetos es obligatoria.")]
    public string ClaveSecreta { get; set; } = string.Empty;

    /// <summary>Contenedor donde se guardan los adjuntos.</summary>
    [Required(ErrorMessage = "El nombre del contenedor es obligatorio.")]
    [RegularExpression("^[a-z0-9][a-z0-9.-]{1,61}[a-z0-9]$", ErrorMessage = "El contenedor debe cumplir las reglas de nombrado de S3: minúsculas, dígitos, punto y guion.")]
    public string Contenedor { get; set; } = "dotchat-adjuntos";

    /// <summary>Región declarada. MinIO la ignora, pero el SDK exige una.</summary>
    [Required(ErrorMessage = "La región es obligatoria.")]
    public string Region { get; set; } = "us-east-1";

    /// <summary>
    /// Usa el estilo de ruta (<c>servidor/contenedor/objeto</c>) en lugar del de
    /// subdominio. MinIO lo necesita, porque un nombre de contenedor no resuelve como
    /// nombre de máquina.
    /// </summary>
    public bool EstiloRuta { get; set; } = true;

    /// <summary>Segundos de espera máximos para una operación contra el almacén.</summary>
    [Range(1, 600, ErrorMessage = "El tiempo de espera debe estar entre 1 y 600 segundos.")]
    public int SegundosTiempoEspera { get; set; } = 60;

    /// <summary>
    /// Crea el contenedor al arrancar si no existe. En producción suele preferirse
    /// crearlo por fuera, con sus políticas y su ciclo de vida.
    /// </summary>
    public bool CrearContenedorSiFalta { get; set; } = true;
}
