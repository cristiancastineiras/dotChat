namespace Chat.Dominio.Entidades;

/// <summary>
/// Pertenencia de un usuario a una sala. Es la base de la autorización de lectura y
/// escritura: solo los miembros pueden leer el historial y publicar mensajes.
/// </summary>
public class MiembroSala
{
    /// <summary>Sala a la que pertenece el usuario.</summary>
    public Guid SalaId { get; set; }

    /// <summary>Sala asociada.</summary>
    public Sala? Sala { get; set; }

    /// <summary>Usuario miembro.</summary>
    public Guid UsuarioId { get; set; }

    /// <summary>Usuario asociado.</summary>
    public Usuario? Usuario { get; set; }

    /// <summary>Fecha UTC en la que el usuario se unió a la sala.</summary>
    public DateTimeOffset FechaUnion { get; set; }

    /// <summary>
    /// Fecha UTC hasta la que el usuario ha leído la conversación. Los mensajes
    /// posteriores se cuentan como pendientes. Nula mientras no haya abierto la sala.
    /// </summary>
    public DateTimeOffset? FechaUltimaLectura { get; set; }
}
