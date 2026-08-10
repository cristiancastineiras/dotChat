using Chat.Dominio.Abstracciones;
using Chat.Dominio.Entidades;
using Microsoft.EntityFrameworkCore;

namespace Chat.Infraestructura.Persistencia.Repositorios;

/// <summary>Implementación de <see cref="IRepositorioSalas"/> sobre EF Core.</summary>
public sealed class RepositorioSalas : IRepositorioSalas
{
    private readonly ContextoChat _contexto;

    /// <summary>Crea el repositorio.</summary>
    /// <param name="contexto">Contexto de EF Core.</param>
    public RepositorioSalas(ContextoChat contexto) => _contexto = contexto;

    /// <inheritdoc />
    public Task<Sala?> ObtenerPorIdAsync(Guid id, CancellationToken cancelacion = default)
        => _contexto.Salas.FirstOrDefaultAsync(s => s.Id == id, cancelacion);

    /// <inheritdoc />
    public Task<Sala?> ObtenerPorNombreAsync(string nombre, CancellationToken cancelacion = default)
        => _contexto.Salas.FirstOrDefaultAsync(
            s => EF.Functions.Like(s.Nombre, nombre),
            cancelacion);

    /// <inheritdoc />
    public Task<Sala?> ObtenerPorClaveDirectaAsync(string claveDirecta, CancellationToken cancelacion = default)
        => _contexto.Salas.FirstOrDefaultAsync(s => s.ClaveDirecta == claveDirecta, cancelacion);

    /// <inheritdoc />
    public async Task<IReadOnlyList<Sala>> ListarAsync(CancellationToken cancelacion = default)
        => await _contexto.Salas
            .AsNoTracking()
            .Include(s => s.Miembros)
            .OrderBy(s => s.Nombre)
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public Task<int> ContarAsync(CancellationToken cancelacion = default)
        => _contexto.Salas.CountAsync(cancelacion);

    /// <inheritdoc />
    public async Task AgregarAsync(Sala sala, CancellationToken cancelacion = default)
        => await _contexto.Salas.AddAsync(sala, cancelacion).ConfigureAwait(false);

    /// <inheritdoc />
    public void Eliminar(Sala sala) => _contexto.Salas.Remove(sala);

    /// <inheritdoc />
    public Task<bool> EsMiembroAsync(Guid salaId, Guid usuarioId, CancellationToken cancelacion = default)
        => _contexto.MiembrosSala.AnyAsync(
            m => m.SalaId == salaId && m.UsuarioId == usuarioId,
            cancelacion);

    /// <inheritdoc />
    public Task<MiembroSala?> ObtenerMembresiaAsync(Guid salaId, Guid usuarioId, CancellationToken cancelacion = default)
        => _contexto.MiembrosSala.FirstOrDefaultAsync(
            m => m.SalaId == salaId && m.UsuarioId == usuarioId,
            cancelacion);

    /// <inheritdoc />
    public async Task AgregarMembresiaAsync(MiembroSala membresia, CancellationToken cancelacion = default)
        => await _contexto.MiembrosSala.AddAsync(membresia, cancelacion).ConfigureAwait(false);

    /// <inheritdoc />
    public void EliminarMembresia(MiembroSala membresia) => _contexto.MiembrosSala.Remove(membresia);

    /// <inheritdoc />
    public Task<int> ContarMiembrosAsync(Guid salaId, CancellationToken cancelacion = default)
        => _contexto.MiembrosSala.CountAsync(m => m.SalaId == salaId, cancelacion);

    /// <inheritdoc />
    public async Task<IReadOnlyList<MiembroSala>> ListarMiembrosAsync(
        Guid salaId,
        CancellationToken cancelacion = default)
        => await _contexto.MiembrosSala
            .AsNoTracking()
            .Include(m => m.Usuario)
            .Where(m => m.SalaId == salaId)
            .OrderBy(m => m.FechaUnion)
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<IReadOnlyList<Guid>> ListarSalasDeUsuarioAsync(Guid usuarioId, CancellationToken cancelacion = default)
        => await _contexto.MiembrosSala
            .AsNoTracking()
            .Where(m => m.UsuarioId == usuarioId)
            .Select(m => m.SalaId)
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<IReadOnlyList<Sala>> ListarDeUsuarioAsync(
        Guid usuarioId,
        CancellationToken cancelacion = default)
        => await _contexto.Salas
            .AsNoTracking()
            .Include(s => s.Miembros)
                .ThenInclude(m => m.Usuario)
            .Where(s => s.Miembros.Any(m => m.UsuarioId == usuarioId))
            .OrderByDescending(s => s.FechaUltimaActividad ?? s.FechaCreacion)
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);
}
