namespace Chat.Aplicacion.Abstracciones;

/// <summary>
/// Protección básica contra repetición (replay): recuerda durante una ventana corta
/// los identificadores de operación ya procesados y rechaza los reenvíos.
/// </summary>
public interface IProtectorRepeticion
{
    /// <summary>
    /// Registra un identificador de operación y devuelve si es la primera vez que se ve.
    /// </summary>
    /// <param name="ambito">Espacio de nombres del identificador (por ejemplo, «mensaje»).</param>
    /// <param name="identificador">Identificador único de la operación.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    /// <returns><c>true</c> si la operación es nueva; <c>false</c> si es una repetición.</returns>
    Task<bool> RegistrarSiEsNuevoAsync(string ambito, string identificador, CancellationToken cancelacion = default);
}
