using Chat.Dominio.Excepciones;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Chat.Servidor.Configuracion;

/// <summary>
/// Traduce las excepciones a respuestas <c>ProblemDetails</c> coherentes.
/// Las excepciones de dominio se convierten en códigos HTTP concretos; cualquier otra
/// se registra íntegra en el servidor y se devuelve genérica al cliente, para no
/// filtrar detalles internos.
/// </summary>
public sealed class ManejadorExcepcionesGlobal : IExceptionHandler
{
    private readonly ILogger<ManejadorExcepcionesGlobal> _registro;
    private readonly IProblemDetailsService _problemas;

    /// <summary>Crea el manejador.</summary>
    /// <param name="registro">Registro estructurado.</param>
    /// <param name="problemas">Servicio de generación de <c>ProblemDetails</c>.</param>
    public ManejadorExcepcionesGlobal(
        ILogger<ManejadorExcepcionesGlobal> registro,
        IProblemDetailsService problemas)
    {
        _registro = registro;
        _problemas = problemas;
    }

    /// <inheritdoc />
    public async ValueTask<bool> TryHandleAsync(
        HttpContext contexto,
        Exception excepcion,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        ArgumentNullException.ThrowIfNull(excepcion);

        var (estado, titulo, detalle, errores) = Clasificar(excepcion);

        if (estado >= StatusCodes.Status500InternalServerError)
        {
            _registro.LogError(
                excepcion,
                "Error no controlado. Ruta={Ruta} Metodo={Metodo} TrazaId={TrazaId}",
                contexto.Request.Path,
                contexto.Request.Method,
                contexto.TraceIdentifier);
        }
        else
        {
            _registro.LogWarning(
                "Petición rechazada. Estado={Estado} Ruta={Ruta} Motivo={Motivo}",
                estado,
                contexto.Request.Path,
                detalle);
        }

        contexto.Response.StatusCode = estado;

        var problema = new ProblemDetails
        {
            Status = estado,
            Title = titulo,
            Detail = detalle,
            Instance = contexto.Request.Path
        };

        problema.Extensions["trazaId"] = contexto.TraceIdentifier;

        if (errores is not null)
        {
            problema.Extensions["errores"] = errores;
        }

        return await _problemas.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = contexto,
            Exception = excepcion,
            ProblemDetails = problema
        }).ConfigureAwait(false);
    }

    /// <summary>Asigna código HTTP, título y detalle a cada tipo de excepción.</summary>
    /// <param name="excepcion">Excepción capturada.</param>
    private static (int Estado, string Titulo, string Detalle, IReadOnlyDictionary<string, string[]>? Errores)
        Clasificar(Exception excepcion) => excepcion switch
        {
            ExcepcionValidacion validacion => (
                StatusCodes.Status400BadRequest,
                "Datos no válidos",
                validacion.Message,
                validacion.Errores),

            ExcepcionAutenticacion autenticacion => (
                StatusCodes.Status401Unauthorized,
                "No autenticado",
                autenticacion.Message,
                null),

            UnauthorizedAccessException => (
                StatusCodes.Status401Unauthorized,
                "No autenticado",
                "Se requiere un token de acceso válido.",
                null),

            ExcepcionAutorizacion autorizacion => (
                StatusCodes.Status403Forbidden,
                "Acceso denegado",
                autorizacion.Message,
                null),

            ExcepcionNoEncontrado noEncontrado => (
                StatusCodes.Status404NotFound,
                "Recurso no encontrado",
                noEncontrado.Message,
                null),

            ExcepcionConflicto conflicto => (
                StatusCodes.Status409Conflict,
                "Conflicto",
                conflicto.Message,
                null),

            OperationCanceledException => (
                StatusCodesExtra.ClientClosedRequest,
                "Petición cancelada",
                "El cliente canceló la petición.",
                null),

            _ => (
                StatusCodes.Status500InternalServerError,
                "Error interno",
                "Se ha producido un error inesperado. Consulte el registro del servidor con el identificador de traza.",
                null)
        };
}

/// <summary>Códigos de estado HTTP no incluidos en <see cref="StatusCodes"/>.</summary>
internal static class StatusCodesExtra
{
    /// <summary>499 Client Closed Request (convención de nginx, útil para cancelaciones).</summary>
    public const int ClientClosedRequest = 499;
}
