namespace Chat.Aplicacion.Abstracciones;

/// <summary>
/// Reconoce si un archivo subido es un audio, para las notas de voz.
/// </summary>
/// <remarks>
/// A diferencia de las imágenes, el audio no se recodifica: los formatos que produce
/// <c>MediaRecorder</c> en el navegador (WebM/Opus, Ogg/Opus) o un archivo de audio
/// cualquiera (MP3, WAV, M4A) se almacenan tal cual llegan, igual que un archivo
/// genérico. Lo único que hace falta es distinguirlos para etiquetar el adjunto como
/// <see cref="Chat.Dominio.Entidades.TipoAdjunto.Audio"/> y que el cliente lo presente
/// como un reproductor y no como una descarga cualquiera. Como con las imágenes, esa
/// distinción se hace mirando la cabecera del contenido y no el nombre ni el tipo MIME
/// que declaró quien lo subió, que no son de fiar.
/// </remarks>
public interface IProcesadorAudio
{
    /// <summary>
    /// Averigua si un contenido es un audio en un formato reconocible, leyendo solo su
    /// cabecera.
    /// </summary>
    /// <param name="origen">Flujo con búsqueda; se devuelve en la posición en que estaba.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    Task<bool> EsAudioAsync(Stream origen, CancellationToken cancelacion = default);
}
