namespace Chat.Dominio.Excepciones;

/// <summary>Se lanza cuando un recurso solicitado no existe. Se traduce a HTTP 404.</summary>
public sealed class ExcepcionNoEncontrado : ExcepcionDominio
{
    /// <summary>Crea la excepción con un mensaje descriptivo.</summary>
    /// <param name="mensaje">Descripción del recurso no encontrado.</param>
    public ExcepcionNoEncontrado(string mensaje) : base(mensaje)
    {
    }

    /// <summary>Construye el mensaje estándar «&lt;recurso&gt; con identificador &lt;id&gt; no existe».</summary>
    /// <param name="recurso">Nombre del recurso.</param>
    /// <param name="identificador">Identificador buscado.</param>
    public static ExcepcionNoEncontrado Para(string recurso, object identificador)
        => new($"{recurso} con identificador '{identificador}' no existe.");
}
