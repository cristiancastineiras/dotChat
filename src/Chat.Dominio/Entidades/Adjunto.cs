namespace Chat.Dominio.Entidades;

/// <summary>
/// Archivo adjunto a un mensaje: una imagen, un documento o cualquier otro contenido.
/// La fila guarda solo la ficha; los bytes viven cifrados en el almacén de objetos.
/// </summary>
/// <remarks>
/// <para>
/// El adjunto se sube antes de existir el mensaje, así que nace huérfano y queda
/// ligado cuando el mensaje se publica. Los que nunca llegan a usarse los retira la
/// tarea de mantenimiento, que borra también su objeto.
/// </para>
/// <para>
/// De las imágenes se conocen las dimensiones porque el servidor las descodifica al
/// recibirlas; del resto de archivos, no, y por eso son opcionales.
/// </para>
/// </remarks>
public class Adjunto
{
    /// <summary>Identificador único del adjunto (UUID v7, ordenable por tiempo).</summary>
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>Sala para la que se subió. Delimita quién puede descargarlo.</summary>
    public Guid SalaId { get; set; }

    /// <summary>Sala asociada.</summary>
    public Sala? Sala { get; set; }

    /// <summary>Usuario que lo subió.</summary>
    public Guid UsuarioId { get; set; }

    /// <summary>Usuario que lo subió.</summary>
    public Usuario? Usuario { get; set; }

    /// <summary>Nombre de archivo saneado, solo para mostrarlo y para la descarga.</summary>
    public required string NombreArchivo { get; set; }

    /// <summary>
    /// Tipo MIME. En las imágenes es el del formato al que las recodificó el servidor;
    /// en el resto, uno deducido de la extensión, nunca el que declaró el cliente.
    /// </summary>
    public required string TipoMime { get; set; }

    /// <summary>Naturaleza del contenido, que decide cómo lo presenta el cliente.</summary>
    public TipoAdjunto Tipo { get; set; } = TipoAdjunto.Archivo;

    /// <summary>Clave del objeto dentro del almacén.</summary>
    public required string ClaveObjeto { get; set; }

    /// <summary>Anchura en píxeles; solo en las imágenes.</summary>
    public int? Ancho { get; set; }

    /// <summary>Altura en píxeles; solo en las imágenes.</summary>
    public int? Alto { get; set; }

    /// <summary>Tamaño en bytes del contenido en claro, antes de cifrarlo.</summary>
    public long TamanoBytes { get; set; }

    /// <summary>
    /// Huella SHA-256 del contenido en claro, en hexadecimal. Permite comprobar en la
    /// descarga que lo que llega es lo que se guardó.
    /// </summary>
    public required string Huella { get; set; }

    /// <summary>Fecha UTC de subida.</summary>
    public DateTimeOffset FechaCreacion { get; set; }

    /// <summary>Mensaje que lo publica; nulo mientras el adjunto sigue sin usarse.</summary>
    public Mensaje? Mensaje { get; set; }

    /// <summary>Indica si el contenido es una imagen que el cliente puede dibujar.</summary>
    public bool EsImagen => Tipo == TipoAdjunto.Imagen;

    /// <summary>
    /// Construye la clave del objeto. Se reparte por sala y por fecha para que el
    /// almacén no acabe con millones de objetos colgando de la raíz, lo que degrada
    /// el listado y el borrado por lotes.
    /// </summary>
    /// <param name="salaId">Sala a la que pertenece.</param>
    /// <param name="adjuntoId">Identificador del adjunto.</param>
    /// <param name="fecha">Fecha de subida.</param>
    public static string ConstruirClave(Guid salaId, Guid adjuntoId, DateTimeOffset fecha)
        => $"salas/{salaId:N}/{fecha:yyyy'/'MM}/{adjuntoId:N}";
}
