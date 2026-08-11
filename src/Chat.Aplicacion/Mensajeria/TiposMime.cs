using System.Collections.Frozen;

namespace Chat.Aplicacion.Mensajeria;

/// <summary>
/// Traduce extensiones de fichero a tipos MIME.
/// </summary>
/// <remarks>
/// <para>
/// El tipo que declara el cliente al subir no se usa nunca: es un dato que controla
/// quien envía y del que depende cómo interpreta el contenido quien lo recibe. El
/// servidor lo deduce él mismo de la extensión ya saneada y, si no la reconoce,
/// devuelve el tipo genérico, que ningún visor va a ejecutar.
/// </para>
/// <para>
/// La tabla es corta a propósito: cubre lo que de verdad se comparte en un chat y no
/// pretende ser un registro completo de tipos.
/// </para>
/// </remarks>
public static class TiposMime
{
    /// <summary>Tipo con el que se sirve todo lo que no se reconoce.</summary>
    public const string Generico = "application/octet-stream";

    /// <summary>Tabla de extensión —con punto y en minúsculas— a tipo MIME.</summary>
    private static readonly FrozenDictionary<string, string> PorExtension = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        // Imágenes
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".gif"] = "image/gif",
        [".webp"] = "image/webp",
        [".bmp"] = "image/bmp",
        [".svg"] = "image/svg+xml",

        // Documentos
        [".pdf"] = "application/pdf",
        [".txt"] = "text/plain",
        [".md"] = "text/markdown",
        [".csv"] = "text/csv",
        [".json"] = "application/json",
        [".xml"] = "application/xml",
        [".yaml"] = "application/yaml",
        [".yml"] = "application/yaml",
        [".log"] = "text/plain",
        [".doc"] = "application/msword",
        [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        [".xls"] = "application/vnd.ms-excel",
        [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        [".ppt"] = "application/vnd.ms-powerpoint",
        [".pptx"] = "application/vnd.openxmlformats-officedocument.presentationml.presentation",

        // Comprimidos
        [".zip"] = "application/zip",
        [".gz"] = "application/gzip",
        [".tar"] = "application/x-tar",
        [".7z"] = "application/x-7z-compressed",
        [".rar"] = "application/vnd.rar",

        // Audio y vídeo
        [".mp3"] = "audio/mpeg",
        [".ogg"] = "audio/ogg",
        [".wav"] = "audio/wav",
        [".m4a"] = "audio/mp4",
        [".mp4"] = "video/mp4",
        [".webm"] = "video/webm",
        [".mkv"] = "video/x-matroska"
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <summary>Deduce el tipo MIME de un nombre de fichero por su extensión.</summary>
    /// <param name="nombreArchivo">Nombre ya saneado.</param>
    /// <returns>El tipo reconocido, o <see cref="Generico"/>.</returns>
    public static string DeducirDe(string? nombreArchivo)
    {
        if (string.IsNullOrWhiteSpace(nombreArchivo))
        {
            return Generico;
        }

        var punto = nombreArchivo.LastIndexOf('.');

        if (punto < 0 || punto == nombreArchivo.Length - 1)
        {
            return Generico;
        }

        return PorExtension.TryGetValue(nombreArchivo[punto..], out var tipo) ? tipo : Generico;
    }
}
