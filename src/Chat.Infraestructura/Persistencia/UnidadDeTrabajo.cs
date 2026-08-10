using Chat.Dominio.Abstracciones;

namespace Chat.Infraestructura.Persistencia;

/// <summary>
/// Unidad de trabajo respaldada por el <see cref="ContextoChat"/>.
/// EF Core ya envuelve <c>SaveChangesAsync</c> en una transacción implícita, por lo que
/// no hace falta gestión manual de transacciones para las operaciones de esta aplicación.
/// </summary>
public sealed class UnidadDeTrabajo : IUnidadDeTrabajo
{
    private readonly ContextoChat _contexto;

    /// <summary>Crea la unidad de trabajo.</summary>
    /// <param name="contexto">Contexto de EF Core.</param>
    public UnidadDeTrabajo(ContextoChat contexto) => _contexto = contexto;

    /// <inheritdoc />
    public Task<int> GuardarCambiosAsync(CancellationToken cancelacion = default)
        => _contexto.SaveChangesAsync(cancelacion);
}
