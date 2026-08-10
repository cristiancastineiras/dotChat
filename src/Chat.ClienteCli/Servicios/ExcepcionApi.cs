using System.Net;

namespace Chat.ClienteCli.Servicios;

/// <summary>
/// Error devuelto por la API del servidor, ya traducido a un mensaje legible.
/// </summary>
public sealed class ExcepcionApi : Exception
{
    /// <summary>Código de estado HTTP recibido.</summary>
    public HttpStatusCode Estado { get; }

    /// <summary>Errores de validación por campo, si el servidor los detalló.</summary>
    public IReadOnlyDictionary<string, string[]> Errores { get; }

    /// <summary>Crea la excepción.</summary>
    /// <param name="estado">Código de estado HTTP.</param>
    /// <param name="mensaje">Mensaje legible.</param>
    /// <param name="errores">Errores por campo.</param>
    public ExcepcionApi(
        HttpStatusCode estado,
        string mensaje,
        IReadOnlyDictionary<string, string[]>? errores = null)
        : base(mensaje)
    {
        Estado = estado;
        Errores = errores ?? new Dictionary<string, string[]>();
    }

    /// <summary>Indica si el fallo se debe a que la sesión no es válida o ha caducado.</summary>
    public bool EsSesionInvalida => Estado is HttpStatusCode.Unauthorized;
}
