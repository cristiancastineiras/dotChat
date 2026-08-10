namespace Chat.Aplicacion.Abstracciones;

/// <summary>
/// Abstrae el reloj del sistema para que la lógica dependiente del tiempo
/// (expiración de tokens, ventanas antirrepetición) sea determinista en pruebas.
/// </summary>
public interface IProveedorFechaHora
{
    /// <summary>Instante actual en UTC.</summary>
    DateTimeOffset Ahora { get; }
}
