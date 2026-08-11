namespace Chat.Dominio.Entidades;

/// <summary>
/// Mensaje publicado en una sala. El contenido nunca se almacena en claro:
/// la propiedad <see cref="TextoCifrado"/> guarda el resultado de AES-256-GCM.
/// </summary>
/// <remarks>
/// Un mensaje lleva texto, una imagen adjunta o ambas cosas, pero nunca ninguna:
/// esa invariante la comprueba la capa de aplicación antes de crearlo.
/// </remarks>
public class Mensaje
{
    /// <summary>Identificador único del mensaje (UUID v7, ordenable por tiempo).</summary>
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>Sala en la que se publicó el mensaje.</summary>
    public Guid SalaId { get; set; }

    /// <summary>Sala asociada.</summary>
    public Sala? Sala { get; set; }

    /// <summary>Autor del mensaje.</summary>
    public Guid UsuarioId { get; set; }

    /// <summary>Usuario autor del mensaje.</summary>
    public Usuario? Usuario { get; set; }

    /// <summary>
    /// Contenido cifrado con AES-256-GCM y codificado en Base64. Es nulo cuando el
    /// mensaje es solo una imagen, sin pie de foto.
    /// </summary>
    public string? TextoCifrado { get; set; }

    /// <summary>Imagen adjunta al mensaje; nulo si el mensaje es solo texto.</summary>
    public Guid? AdjuntoId { get; set; }

    /// <summary>Imagen adjunta.</summary>
    public Adjunto? Adjunto { get; set; }

    /// <summary>Fecha UTC de envío del mensaje.</summary>
    public DateTimeOffset FechaEnvio { get; set; }
}
