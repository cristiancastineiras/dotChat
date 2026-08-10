using Chat.Dominio.Abstracciones;
using Chat.Dominio.Entidades;
using Microsoft.EntityFrameworkCore;

namespace Chat.Infraestructura.Persistencia.Repositorios;

/// <summary>Implementación de <see cref="IRepositorioMensajes"/> sobre EF Core.</summary>
public sealed class RepositorioMensajes : IRepositorioMensajes
{
    private readonly ContextoChat _contexto;

    /// <summary>Crea el repositorio.</summary>
    /// <param name="contexto">Contexto de EF Core.</param>
    public RepositorioMensajes(ContextoChat contexto) => _contexto = contexto;

    /// <inheritdoc />
    public async Task AgregarAsync(Mensaje mensaje, CancellationToken cancelacion = default)
        => await _contexto.Mensajes.AddAsync(mensaje, cancelacion).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<IReadOnlyList<Mensaje>> ObtenerRecientesAsync(
        Guid salaId,
        int cantidad,
        DateTimeOffset? anteriorA,
        CancellationToken cancelacion = default)
    {
        var consulta = _contexto.Mensajes
            .AsNoTracking()
            .Include(m => m.Usuario)
            .Where(m => m.SalaId == salaId);

        if (anteriorA is not null)
        {
            consulta = consulta.Where(m => m.FechaEnvio < anteriorA.Value);
        }

        // Se toman los N más recientes con el índice descendente y después se
        // invierte el orden para devolverlos en secuencia cronológica de lectura.
        var pagina = await consulta
            .OrderByDescending(m => m.FechaEnvio)
            .ThenByDescending(m => m.Id)
            .Take(cantidad)
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        pagina.Reverse();
        return pagina;
    }

    /// <inheritdoc />
    public Task<int> ContarAsync(Guid? salaId, CancellationToken cancelacion = default)
        => salaId is null
            ? _contexto.Mensajes.CountAsync(cancelacion)
            : _contexto.Mensajes.CountAsync(m => m.SalaId == salaId.Value, cancelacion);

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<Guid, int>> ContarNoLeidosPorSalaAsync(
        Guid usuarioId,
        CancellationToken cancelacion = default)
    {
        // Los mensajes propios nunca cuentan como pendientes; una marca de lectura
        // nula significa que el usuario no ha abierto la sala desde que se unió.
        //
        // Se escribe como reunión explícita y no como subconsulta correlacionada
        // porque esta última obligaría a SQLite a usar APPLY, que no soporta. Así se
        // traduce a un INNER JOIN con GROUP BY, que además aprovecha los índices.
        var consulta =
            from mensaje in _contexto.Mensajes.AsNoTracking()
            join miembro in _contexto.MiembrosSala.AsNoTracking()
                on mensaje.SalaId equals miembro.SalaId
            where miembro.UsuarioId == usuarioId
                && mensaje.UsuarioId != usuarioId
                && (miembro.FechaUltimaLectura == null || mensaje.FechaEnvio > miembro.FechaUltimaLectura)
            group mensaje by mensaje.SalaId into grupo
            select new { SalaId = grupo.Key, Total = grupo.Count() };

        var pendientes = await consulta.ToListAsync(cancelacion).ConfigureAwait(false);

        return pendientes.ToDictionary(p => p.SalaId, p => p.Total);
    }
}
