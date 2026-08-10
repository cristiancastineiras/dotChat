namespace Chat.Dominio.Excepciones;

/// <summary>
/// Se lanza cuando los datos de entrada no cumplen las reglas de validación.
/// Se traduce a HTTP 400 con el detalle de los errores por campo.
/// </summary>
public sealed class ExcepcionValidacion : ExcepcionDominio
{
    /// <summary>Errores agrupados por nombre de campo.</summary>
    public IReadOnlyDictionary<string, string[]> Errores { get; }

    /// <summary>Crea la excepción a partir de un único error.</summary>
    /// <param name="campo">Campo que falló la validación.</param>
    /// <param name="mensaje">Motivo del fallo.</param>
    public ExcepcionValidacion(string campo, string mensaje)
        : base(mensaje)
    {
        Errores = new Dictionary<string, string[]> { [campo] = [mensaje] };
    }

    /// <summary>Crea la excepción a partir de un conjunto de errores.</summary>
    /// <param name="errores">Errores agrupados por campo.</param>
    public ExcepcionValidacion(IReadOnlyDictionary<string, string[]> errores)
        : base("Los datos proporcionados no son válidos.")
    {
        Errores = errores;
    }
}
