namespace Chat.Dominio.Excepciones;

/// <summary>
/// Se lanza cuando las credenciales o el token presentados no son válidos.
/// Se traduce a HTTP 401. El mensaje es deliberadamente genérico para no
/// revelar si el fallo fue por usuario inexistente o por contraseña incorrecta.
/// </summary>
public sealed class ExcepcionAutenticacion : ExcepcionDominio
{
    /// <summary>Crea la excepción con un mensaje descriptivo.</summary>
    /// <param name="mensaje">Motivo genérico del fallo de autenticación.</param>
    public ExcepcionAutenticacion(string mensaje) : base(mensaje)
    {
    }
}
