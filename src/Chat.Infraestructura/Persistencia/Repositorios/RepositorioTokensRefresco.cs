using Chat.Dominio.Abstracciones;
using Chat.Dominio.Entidades;
using Microsoft.EntityFrameworkCore;

namespace Chat.Infraestructura.Persistencia.Repositorios;

/// <summary>Implementación de <see cref="IRepositorioTokensRefresco"/> sobre EF Core.</summary>
public sealed class RepositorioTokensRefresco : IRepositorioTokensRefresco
{
    private readonly ContextoChat _contexto;

    /// <summary>Crea el repositorio.</summary>
    /// <param name="contexto">Contexto de EF Core.</param>
    public RepositorioTokensRefresco(ContextoChat contexto) => _contexto = contexto;

    /// <inheritdoc />
    public async Task AgregarAsync(TokenRefresco token, CancellationToken cancelacion = default)
        => await _contexto.TokensRefresco.AddAsync(token, cancelacion).ConfigureAwait(false);

    /// <inheritdoc />
    public Task<TokenRefresco?> ObtenerPorHashAsync(string hash, CancellationToken cancelacion = default)
        => _contexto.TokensRefresco.FirstOrDefaultAsync(t => t.HashToken == hash, cancelacion);

    /// <inheritdoc />
    public async Task RevocarTodosAsync(Guid usuarioId, DateTimeOffset ahora, CancellationToken cancelacion = default)
    {
        var activos = await _contexto.TokensRefresco
            .Where(t => t.UsuarioId == usuarioId && t.FechaRevocacion == null)
            .ToListAsync(cancelacion)
            .ConfigureAwait(false);

        foreach (var token in activos)
        {
            token.Revocar(ahora);
        }
    }

    /// <inheritdoc />
    public Task<int> PurgarAsync(DateTimeOffset limite, CancellationToken cancelacion = default)
        => _contexto.TokensRefresco
            .Where(t => t.FechaExpiracion < limite || t.FechaRevocacion != null)
            .ExecuteDeleteAsync(cancelacion);
}
