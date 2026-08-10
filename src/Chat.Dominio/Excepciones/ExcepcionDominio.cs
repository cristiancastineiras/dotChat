namespace Chat.Dominio.Excepciones;

/// <summary>
/// Excepción base de las reglas de negocio. Permite que la capa de presentación
/// traduzca los errores esperados a códigos HTTP sin depender de tipos concretos.
/// </summary>
public abstract class ExcepcionDominio : Exception
{
    /// <summary>Crea la excepción con un mensaje descriptivo.</summary>
    /// <param name="mensaje">Mensaje apto para mostrar al usuario final.</param>
    protected ExcepcionDominio(string mensaje) : base(mensaje)
    {
    }
}
