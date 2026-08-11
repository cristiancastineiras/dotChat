using System.ComponentModel.DataAnnotations;

namespace Chat.Aplicacion.Opciones;

/// <summary>
/// Configuración del segundo nivel de caché, alojado en Valkey.
/// </summary>
/// <remarks>
/// Valkey habla el protocolo de Redis, así que se conecta con el cliente estándar
/// y la cadena tiene el formato habitual <c>servidor:puerto[,opción=valor]</c>.
/// El nivel distribuido es opcional: si se desactiva —o si Valkey no responde—,
/// la aplicación sigue funcionando solo con la caché en memoria del proceso.
/// </remarks>
public sealed class ValkeyOptions
{
    /// <summary>Nombre de la sección en appsettings.json.</summary>
    public const string Seccion = "Valkey";

    /// <summary>
    /// Activa el segundo nivel de caché y el canal de retropropagación.
    /// Desactivarlo deja la caché reducida a la memoria del proceso, que es
    /// suficiente para una instancia única.
    /// </summary>
    public bool Activado { get; set; } = true;

    /// <summary>Cadena de conexión al servidor de Valkey.</summary>
    [Required(ErrorMessage = "La cadena de conexión de Valkey es obligatoria cuando el nivel distribuido está activo.")]
    public string Conexion { get; set; } = "localhost:6379";

    /// <summary>
    /// Prefijo que antecede a todas las claves y nombra el canal de retropropagación.
    /// Permite compartir una misma instancia de Valkey entre varios entornos sin
    /// que se pisen las entradas.
    /// </summary>
    [Required(ErrorMessage = "El prefijo de las claves de Valkey es obligatorio.")]
    [RegularExpression("^[a-zA-Z0-9._:-]+$", ErrorMessage = "El prefijo solo admite letras, dígitos, punto, guion, guion bajo y dos puntos.")]
    public string Prefijo { get; set; } = "dotchat";

    /// <summary>Tiempo máximo, en milisegundos, para establecer la conexión o completar una operación.</summary>
    [Range(100, 30000, ErrorMessage = "El tiempo de espera debe estar entre 100 y 30000 milisegundos.")]
    public int MilisegundosTiempoEspera { get; set; } = 2000;

    /// <summary>
    /// Tiempo máximo, en milisegundos, que se espera al segundo nivel antes de
    /// resolver con lo que haya en memoria. Evita que una Valkey lenta se note en
    /// la latencia de las peticiones.
    /// </summary>
    [Range(50, 10000, ErrorMessage = "El margen blando debe estar entre 50 y 10000 milisegundos.")]
    public int MilisegundosMargenBlando { get; set; } = 200;

    /// <summary>Construye el nombre del canal de retropropagación a partir del prefijo.</summary>
    public string CanalRetropropagacion() => $"{Prefijo}:backplane";

    /// <summary>Construye el prefijo de las claves del segundo nivel.</summary>
    public string PrefijoClaves() => $"{Prefijo}:";
}
