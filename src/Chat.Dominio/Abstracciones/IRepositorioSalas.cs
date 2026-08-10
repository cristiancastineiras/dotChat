using Chat.Dominio.Entidades;

namespace Chat.Dominio.Abstracciones;

/// <summary>Acceso a datos de la entidad <see cref="Sala"/> y de sus membresías.</summary>
public interface IRepositorioSalas
{
    /// <summary>Obtiene una sala por su identificador.</summary>
    /// <param name="id">Identificador de la sala.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    Task<Sala?> ObtenerPorIdAsync(Guid id, CancellationToken cancelacion = default);

    /// <summary>Obtiene una sala por su nombre (comparación insensible a mayúsculas).</summary>
    /// <param name="nombre">Nombre de la sala.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    Task<Sala?> ObtenerPorNombreAsync(string nombre, CancellationToken cancelacion = default);

    /// <summary>Obtiene la conversación directa identificada por su clave canónica.</summary>
    /// <param name="claveDirecta">Clave devuelta por <see cref="Sala.ConstruirClaveDirecta"/>.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    Task<Sala?> ObtenerPorClaveDirectaAsync(string claveDirecta, CancellationToken cancelacion = default);

    /// <summary>Lista todas las salas ordenadas por nombre.</summary>
    /// <param name="cancelacion">Token de cancelación.</param>
    Task<IReadOnlyList<Sala>> ListarAsync(CancellationToken cancelacion = default);

    /// <summary>Cuenta las salas existentes.</summary>
    /// <param name="cancelacion">Token de cancelación.</param>
    Task<int> ContarAsync(CancellationToken cancelacion = default);

    /// <summary>Añade una sala nueva al contexto.</summary>
    /// <param name="sala">Sala a añadir.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    Task AgregarAsync(Sala sala, CancellationToken cancelacion = default);

    /// <summary>Marca una sala para eliminación (elimina en cascada mensajes y membresías).</summary>
    /// <param name="sala">Sala a eliminar.</param>
    void Eliminar(Sala sala);

    /// <summary>Indica si un usuario es miembro de una sala.</summary>
    /// <param name="salaId">Identificador de la sala.</param>
    /// <param name="usuarioId">Identificador del usuario.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    Task<bool> EsMiembroAsync(Guid salaId, Guid usuarioId, CancellationToken cancelacion = default);

    /// <summary>Obtiene la membresía de un usuario en una sala, si existe.</summary>
    /// <param name="salaId">Identificador de la sala.</param>
    /// <param name="usuarioId">Identificador del usuario.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    Task<MiembroSala?> ObtenerMembresiaAsync(Guid salaId, Guid usuarioId, CancellationToken cancelacion = default);

    /// <summary>Añade una membresía nueva.</summary>
    /// <param name="membresia">Membresía a registrar.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    Task AgregarMembresiaAsync(MiembroSala membresia, CancellationToken cancelacion = default);

    /// <summary>Elimina una membresía existente.</summary>
    /// <param name="membresia">Membresía a eliminar.</param>
    void EliminarMembresia(MiembroSala membresia);

    /// <summary>Cuenta los miembros de una sala.</summary>
    /// <param name="salaId">Identificador de la sala.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    Task<int> ContarMiembrosAsync(Guid salaId, CancellationToken cancelacion = default);

    /// <summary>
    /// Lista los miembros de una sala con su usuario cargado, para mostrar la
    /// composición de la conversación.
    /// </summary>
    /// <param name="salaId">Identificador de la sala.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    Task<IReadOnlyList<MiembroSala>> ListarMiembrosAsync(Guid salaId, CancellationToken cancelacion = default);

    /// <summary>Obtiene los identificadores de las salas a las que pertenece un usuario.</summary>
    /// <param name="usuarioId">Identificador del usuario.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    Task<IReadOnlyList<Guid>> ListarSalasDeUsuarioAsync(Guid usuarioId, CancellationToken cancelacion = default);

    /// <summary>
    /// Lista las salas de un usuario con sus miembros y los usuarios de estos ya
    /// cargados. Es la consulta que alimenta la bandeja del cliente: de ahí salen el
    /// nombre del interlocutor de cada conversación directa y la marca de lectura.
    /// </summary>
    /// <param name="usuarioId">Identificador del usuario.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    Task<IReadOnlyList<Sala>> ListarDeUsuarioAsync(Guid usuarioId, CancellationToken cancelacion = default);
}
