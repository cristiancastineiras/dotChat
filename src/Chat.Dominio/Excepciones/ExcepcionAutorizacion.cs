namespace Chat.Dominio.Excepciones;

/// <summary>
/// Se lanza cuando el usuario está autenticado pero no tiene permiso para la
/// operación solicitada. Se traduce a HTTP 403.
/// </summary>
public sealed class ExcepcionAutorizacion : ExcepcionDominio
{
    /// <summary>Crea la excepción con un mensaje descriptivo.</summary>
    /// <param name="mensaje">Motivo de la denegación.</param>
    public ExcepcionAutorizacion(string mensaje) : base(mensaje)
    {
    }
}
