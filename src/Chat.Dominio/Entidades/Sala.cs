using System.Globalization;

namespace Chat.Dominio.Entidades;

/// <summary>
/// Sala de conversación. Agrupa a los miembros y a los mensajes intercambiados.
/// Una conversación directa entre dos personas es también una sala, de tipo
/// <see cref="TipoSala.Directa"/>, para reutilizar sin duplicar el cifrado, el
/// historial y la difusión en tiempo real.
/// </summary>
public class Sala
{
    /// <summary>Identificador único de la sala.</summary>
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>Nombre único de la sala (visible para los usuarios).</summary>
    public required string Nombre { get; set; }

    /// <summary>Descripción opcional del propósito de la sala.</summary>
    public string? Descripcion { get; set; }

    /// <summary>Naturaleza de la sala: pública, privada o conversación directa.</summary>
    public TipoSala Tipo { get; set; } = TipoSala.Publica;

    /// <summary>
    /// Clave canónica de una conversación directa, formada por los dos identificadores
    /// de usuario ordenados. Es única y permite reabrir siempre la misma conversación
    /// entre dos personas en lugar de crear una nueva. Nula en el resto de salas.
    /// </summary>
    public string? ClaveDirecta { get; set; }

    /// <summary>Fecha UTC de creación de la sala.</summary>
    public DateTimeOffset FechaCreacion { get; set; }

    /// <summary>
    /// Fecha UTC del último mensaje publicado. Permite ordenar la lista de salas por
    /// actividad reciente sin recorrer la tabla de mensajes.
    /// </summary>
    public DateTimeOffset? FechaUltimaActividad { get; set; }

    /// <summary>Identificador del usuario que creó la sala; nulo si el usuario fue eliminado.</summary>
    public Guid? CreadorId { get; set; }

    /// <summary>Usuario que creó la sala.</summary>
    public Usuario? Creador { get; set; }

    /// <summary>Mensajes publicados en la sala.</summary>
    public ICollection<Mensaje> Mensajes { get; set; } = [];

    /// <summary>Miembros actuales de la sala.</summary>
    public ICollection<MiembroSala> Miembros { get; set; } = [];

    /// <summary>
    /// Construye la clave canónica de la conversación directa entre dos usuarios.
    /// El orden de los argumentos es indiferente: siempre devuelve la misma clave,
    /// que es lo que garantiza que no se dupliquen conversaciones.
    /// </summary>
    /// <param name="primerUsuarioId">Identificador de uno de los participantes.</param>
    /// <param name="segundoUsuarioId">Identificador del otro participante.</param>
    public static string ConstruirClaveDirecta(Guid primerUsuarioId, Guid segundoUsuarioId)
    {
        var (menor, mayor) = primerUsuarioId.CompareTo(segundoUsuarioId) <= 0
            ? (primerUsuarioId, segundoUsuarioId)
            : (segundoUsuarioId, primerUsuarioId);

        return string.Create(CultureInfo.InvariantCulture, $"{menor:N}:{mayor:N}");
    }

    /// <summary>
    /// Nombre interno de una conversación directa. No se muestra nunca al usuario
    /// (los clientes presentan el nombre del interlocutor), pero mantiene la
    /// restricción de unicidad de la columna sin colisionar con salas normales.
    /// </summary>
    /// <param name="claveDirecta">Clave canónica de la conversación.</param>
    public static string ConstruirNombreDirecto(string claveDirecta)
        => string.Create(CultureInfo.InvariantCulture, $"directa:{claveDirecta}");
}
