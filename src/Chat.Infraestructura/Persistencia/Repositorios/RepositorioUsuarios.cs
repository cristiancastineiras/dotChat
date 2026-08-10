using Chat.Dominio.Abstracciones;
using Chat.Dominio.Entidades;
using Microsoft.EntityFrameworkCore;

namespace Chat.Infraestructura.Persistencia.Repositorios;

/// <summary>Implementación de <see cref="IRepositorioUsuarios"/> sobre EF Core.</summary>
public sealed class RepositorioUsuarios : IRepositorioUsuarios
{
    private readonly ContextoChat _contexto;

    /// <summary>Crea el repositorio.</summary>
    /// <param name="contexto">Contexto de EF Core.</param>
    public RepositorioUsuarios(ContextoChat contexto) => _contexto = contexto;

    /// <inheritdoc />
    public Task<Usuario?> ObtenerPorIdAsync(Guid id, CancellationToken cancelacion = default)
        => _contexto.Users.FirstOrDefaultAsync(u => u.Id == id, cancelacion);

    /// <inheritdoc />
    public Task<Usuario?> ObtenerPorNombreAsync(string nombreUsuario, CancellationToken cancelacion = default)
    {
        // Identity almacena el nombre normalizado en mayúsculas invariantes; se busca
        // por ese campo para que la comparación use el índice único existente.
        var normalizado = nombreUsuario.ToUpperInvariant();
        return _contexto.Users.FirstOrDefaultAsync(u => u.NormalizedUserName == normalizado, cancelacion);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Usuario>> ListarAsync(bool incluirInactivos, CancellationToken cancelacion = default)
    {
        var consulta = _contexto.Users.AsNoTracking();

        if (!incluirInactivos)
        {
            consulta = consulta.Where(u => u.Activo);
        }

        return await consulta
            .OrderBy(u => u.UserName)
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<int> ContarAsync(CancellationToken cancelacion = default)
        => _contexto.Users.CountAsync(cancelacion);

    /// <inheritdoc />
    public void Eliminar(Usuario usuario) => _contexto.Users.Remove(usuario);
}
