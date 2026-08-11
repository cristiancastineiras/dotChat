using Chat.Dominio.Abstracciones;
using Chat.Dominio.Entidades;
using Microsoft.EntityFrameworkCore;

namespace Chat.Infraestructura.Persistencia.Repositorios;

/// <summary>Implementación de <see cref="IRepositorioAdjuntos"/> sobre EF Core.</summary>
public sealed class RepositorioAdjuntos : IRepositorioAdjuntos
{
    /// <summary>Número máximo de huérfanos que se purgan en una sola pasada.</summary>
    private const int LotePurga = 500;

    private readonly ContextoChat _contexto;

    /// <summary>Crea el repositorio.</summary>
    /// <param name="contexto">Contexto de EF Core.</param>
    public RepositorioAdjuntos(ContextoChat contexto) => _contexto = contexto;

    /// <inheritdoc />
    public async Task AgregarAsync(Adjunto adjunto, CancellationToken cancelacion = default)
        => await _contexto.Adjuntos.AddAsync(adjunto, cancelacion).ConfigureAwait(false);

    /// <inheritdoc />
    public Task<Adjunto?> ObtenerPorIdAsync(Guid adjuntoId, CancellationToken cancelacion = default)
        => _contexto.Adjuntos
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == adjuntoId, cancelacion);

    /// <inheritdoc />
    /// <remarks>
    /// Se pregunta por el lado del mensaje, que es donde vive la clave ajena y tiene
    /// índice único: una sola comprobación de existencia sin leer ninguna fila.
    /// </remarks>
    public Task<bool> EstaPublicadoAsync(Guid adjuntoId, CancellationToken cancelacion = default)
        => _contexto.Mensajes.AnyAsync(m => m.AdjuntoId == adjuntoId, cancelacion);

    /// <inheritdoc />
    public Task<int> ContarAsync(CancellationToken cancelacion = default)
        => _contexto.Adjuntos.CountAsync(cancelacion);

    /// <inheritdoc />
    public async Task<long> SumarTamanoAsync(CancellationToken cancelacion = default)
        => await _contexto.Adjuntos
            .SumAsync(a => (long?)a.TamanoBytes, cancelacion)
            .ConfigureAwait(false) ?? 0;

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> PurgarHuerfanosAsync(
        DateTimeOffset anteriorA,
        CancellationToken cancelacion = default)
    {
        var huerfanos = _contexto.Adjuntos
            .Where(a => a.Mensaje == null && a.FechaCreacion < anteriorA)
            .OrderBy(a => a.FechaCreacion)
            .Take(LotePurga);

        var claves = await huerfanos
            .Select(a => a.ClaveObjeto)
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        if (claves.Count == 0)
        {
            return [];
        }

        // Se borra en el servidor con una sola sentencia, sin materializar las
        // entidades solo para marcarlas como eliminadas.
        await _contexto.Adjuntos
            .Where(a => claves.Contains(a.ClaveObjeto))
            .ExecuteDeleteAsync(cancelacion)
            .ConfigureAwait(false);

        return claves;
    }
}
