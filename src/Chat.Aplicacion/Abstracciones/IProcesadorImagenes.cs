namespace Chat.Aplicacion.Abstracciones;

/// <summary>Imagen ya validada, reescalada y recodificada por el servidor.</summary>
/// <param name="Datos">Bytes de la imagen normalizada.</param>
/// <param name="TipoMime">Tipo MIME real del formato de salida.</param>
/// <param name="Extension">Extensión que corresponde al formato de salida, con punto.</param>
/// <param name="Ancho">Anchura en píxeles.</param>
/// <param name="Alto">Altura en píxeles.</param>
public sealed record ImagenNormalizada(
    byte[] Datos,
    string TipoMime,
    string Extension,
    int Ancho,
    int Alto);

/// <summary>
/// Valida y normaliza las imágenes que llegan al servidor antes de almacenarlas.
/// </summary>
/// <remarks>
/// <para>
/// Que un fichero se llame <c>.png</c> no significa nada: el formato se determina
/// descodificando el contenido. Lo que se guarda es siempre el resultado de volver a
/// codificar la imagen, no el fichero original, de modo que no se persiste nada que no
/// sea una imagen legítima y se pierden por el camino los metadatos EXIF —con su
/// geolocalización y su modelo de cámara— que el remitente probablemente no sabía que
/// estaba compartiendo.
/// </para>
/// <para>
/// Los archivos que no son imágenes no pasan por aquí: no hay forma de recodificar un
/// documento arbitrario, así que se guardan tal cual y la defensa se traslada a cómo
/// se sirven, siempre como descarga y con un tipo que ningún visor ejecuta.
/// </para>
/// </remarks>
public interface IProcesadorImagenes
{
    /// <summary>
    /// Averigua si un contenido es una imagen que este servidor sabe tratar, leyendo
    /// solo su cabecera.
    /// </summary>
    /// <param name="origen">Flujo con búsqueda; se devuelve en la posición en que estaba.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    Task<bool> EsImagenAsync(Stream origen, CancellationToken cancelacion = default);

    /// <summary>Valida, reescala y recodifica una imagen recibida.</summary>
    /// <param name="origen">Flujo con el contenido original.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    /// <returns>La imagen normalizada, lista para cifrar y persistir.</returns>
    /// <exception cref="Chat.Dominio.Excepciones.ExcepcionValidacion">
    /// Si el contenido no es una imagen reconocible o excede los límites configurados.
    /// </exception>
    Task<ImagenNormalizada> NormalizarAsync(Stream origen, CancellationToken cancelacion = default);
}
