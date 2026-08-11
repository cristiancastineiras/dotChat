using System.ComponentModel.DataAnnotations;

namespace Chat.Aplicacion.Opciones;

/// <summary>
/// Límites aplicados a los archivos que se adjuntan a los mensajes.
/// </summary>
/// <remarks>
/// Son el cortafuegos frente a los abusos clásicos de una subida: agotar el almacén con
/// ficheros enormes y agotar la memoria con una imagen que ocupa poco comprimida pero
/// se expande a gigabytes al descodificarla. Los archivos que no son imágenes se pasan
/// en flujo y nunca se cargan enteros, así que admiten un tope más generoso.
/// </remarks>
public sealed class AdjuntosOptions
{
    /// <summary>Nombre de la sección en appsettings.json.</summary>
    public const string Seccion = "Adjuntos";

    /// <summary>Permite adjuntar archivos. Desactivado, la ruta de subida responde 403.</summary>
    public bool Activados { get; set; } = true;

    /// <summary>Tamaño máximo de un archivo cualquiera, en bytes.</summary>
    [Range(1024, 1073741824, ErrorMessage = "El tamaño máximo de un adjunto debe estar entre 1 KiB y 1 GiB.")]
    public long TamanoMaximoBytes { get; set; } = 25L * 1024 * 1024;

    /// <summary>
    /// Tamaño máximo de una imagen, en bytes. Es más bajo que el de un archivo
    /// cualquiera porque una imagen sí se descodifica entera en memoria para
    /// normalizarla.
    /// </summary>
    [Range(1024, 134217728, ErrorMessage = "El tamaño máximo de una imagen debe estar entre 1 KiB y 128 MiB.")]
    public long TamanoMaximoImagenBytes { get; set; } = 8L * 1024 * 1024;

    /// <summary>
    /// Lado máximo, en píxeles, de la imagen almacenada. Lo que llega más grande se
    /// reescala manteniendo la proporción: nadie va a mirar más resolución en una
    /// consola, y así el almacenamiento queda acotado.
    /// </summary>
    [Range(64, 8192, ErrorMessage = "El lado máximo debe estar entre 64 y 8192 píxeles.")]
    public int LadoMaximoPixeles { get; set; } = 1600;

    /// <summary>
    /// Superficie máxima, en megapíxeles, que se acepta descodificar. Se comprueba
    /// leyendo solo la cabecera del fichero, antes de reservar memoria para el mapa
    /// de bits: es lo que frena las «bombas de descompresión».
    /// </summary>
    [Range(1, 500, ErrorMessage = "El límite de megapíxeles debe estar entre 1 y 500.")]
    public int MegapixelesMaximos { get; set; } = 40;

    /// <summary>Calidad de recodificación JPEG, entre 1 y 100.</summary>
    [Range(1, 100, ErrorMessage = "La calidad JPEG debe estar entre 1 y 100.")]
    public int CalidadJpeg { get; set; } = 82;

    /// <summary>Horas que sobrevive un adjunto subido que nunca llegó a publicarse.</summary>
    [Range(1, 720, ErrorMessage = "El margen de los huérfanos debe estar entre 1 y 720 horas.")]
    public int HorasMargenHuerfanos { get; set; } = 2;

    /// <summary>Superficie máxima expresada en píxeles.</summary>
    public long PixelesMaximos() => (long)MegapixelesMaximos * 1_000_000;
}
