namespace Chat.Dominio.Excepciones;

/// <summary>
/// Se lanza cuando la operación choca con el estado actual del sistema
/// (por ejemplo, una sala con nombre duplicado). Se traduce a HTTP 409.
/// </summary>
public sealed class ExcepcionConflicto : ExcepcionDominio
{
    /// <summary>Crea la excepción con un mensaje descriptivo.</summary>
    /// <param name="mensaje">Descripción del conflicto.</param>
    public ExcepcionConflicto(string mensaje) : base(mensaje)
    {
    }
}
