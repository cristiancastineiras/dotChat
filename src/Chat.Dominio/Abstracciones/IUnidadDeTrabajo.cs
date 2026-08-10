namespace Chat.Dominio.Abstracciones;

/// <summary>
/// Unidad de trabajo simple: confirma en una sola transacción todos los cambios
/// acumulados por los repositorios durante la operación en curso.
/// </summary>
public interface IUnidadDeTrabajo
{
    /// <summary>Persiste los cambios pendientes.</summary>
    /// <param name="cancelacion">Token de cancelación.</param>
    /// <returns>Número de filas afectadas.</returns>
    Task<int> GuardarCambiosAsync(CancellationToken cancelacion = default);
}
