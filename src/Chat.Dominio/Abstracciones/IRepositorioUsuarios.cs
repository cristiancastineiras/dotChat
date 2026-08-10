using Chat.Dominio.Entidades;

namespace Chat.Dominio.Abstracciones;

/// <summary>Acceso a datos de la entidad <see cref="Usuario"/>.</summary>
public interface IRepositorioUsuarios
{
    /// <summary>Obtiene un usuario por su identificador.</summary>
    /// <param name="id">Identificador del usuario.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    Task<Usuario?> ObtenerPorIdAsync(Guid id, CancellationToken cancelacion = default);

    /// <summary>Obtiene un usuario por su nombre de usuario (comparación insensible a mayúsculas).</summary>
    /// <param name="nombreUsuario">Nombre de usuario.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    Task<Usuario?> ObtenerPorNombreAsync(string nombreUsuario, CancellationToken cancelacion = default);

    /// <summary>Lista los usuarios ordenados por nombre.</summary>
    /// <param name="incluirInactivos">Incluye también las cuentas desactivadas.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    Task<IReadOnlyList<Usuario>> ListarAsync(bool incluirInactivos, CancellationToken cancelacion = default);

    /// <summary>Cuenta los usuarios registrados.</summary>
    /// <param name="cancelacion">Token de cancelación.</param>
    Task<int> ContarAsync(CancellationToken cancelacion = default);

    /// <summary>Marca un usuario para eliminación.</summary>
    /// <param name="usuario">Usuario a eliminar.</param>
    void Eliminar(Usuario usuario);
}
