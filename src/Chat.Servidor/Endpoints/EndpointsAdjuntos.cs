using System.Net.Mime;
using System.Security.Claims;
using Chat.Aplicacion.Comandos.Mensajes;
using Chat.Aplicacion.Consultas.Mensajes;
using Chat.Aplicacion.Cqrs;
using Chat.Aplicacion.Opciones;
using Chat.Dominio.Excepciones;
using Chat.Servidor.Configuracion;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace Chat.Servidor.Endpoints;

/// <summary>Endpoints de subida y descarga de los archivos adjuntos a los mensajes.</summary>
/// <remarks>
/// Los archivos viajan por HTTP y no por el hub: SignalR está afinado para mensajes
/// pequeños y frecuentes, y meter megabytes por ese canal bloquearía la conversación
/// de todos los miembros de la sala mientras dura la transferencia.
/// </remarks>
public static class EndpointsAdjuntos
{
    /// <summary>Nombre del campo del formulario que transporta el fichero.</summary>
    private const string CampoArchivo = "archivo";

    /// <summary>
    /// Nombre del campo opcional con la duración de una nota de voz, en milisegundos.
    /// </summary>
    private const string CampoDuracionMs = "duracionMs";

    /// <summary>Registra el grupo <c>/api/adjuntos</c>.</summary>
    /// <param name="rutas">Constructor de rutas.</param>
    /// <returns>El mismo constructor, para encadenar llamadas.</returns>
    public static IEndpointRouteBuilder MapearEndpointsAdjuntos(this IEndpointRouteBuilder rutas)
    {
        ArgumentNullException.ThrowIfNull(rutas);

        var grupo = rutas.MapGroup("/api/adjuntos")
            .WithTags("Adjuntos")
            .RequireAuthorization(ExtensionesAutenticacion.PoliticaUsuarioAutenticado);

        grupo.MapPost("/", SubirAsync)
            .WithName("SubirAdjunto")
            .WithSummary("Sube un archivo a una sala y devuelve el identificador con el que publicarlo.")
            .RequireRateLimiting(ExtensionesLimitacionPeticiones.PoliticaSubida)
            .DisableAntiforgery();

        grupo.MapGet("/{adjuntoId:guid}", DescargarAsync)
            .WithName("DescargarAdjunto")
            .WithSummary("Devuelve el contenido descifrado de un archivo adjunto.")
            .RequireRateLimiting(ExtensionesLimitacionPeticiones.PoliticaDescarga);

        return rutas;
    }

    /// <summary>Recibe el archivo como formulario multiparte y lo deja listo para publicarse.</summary>
    /// <param name="salaId">Sala para la que se sube.</param>
    /// <param name="peticion">Petición HTTP, de la que se lee el formulario.</param>
    /// <param name="principal">Identidad de la petición.</param>
    /// <param name="despachador">Despachador CQRS.</param>
    /// <param name="opciones">Límites configurados para los adjuntos.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    private static async Task<IResult> SubirAsync(
        Guid salaId,
        HttpRequest peticion,
        ClaimsPrincipal principal,
        IDespachador despachador,
        IOptions<AdjuntosOptions> opciones,
        CancellationToken cancelacion)
    {
        var limites = opciones.Value;

        if (!peticion.HasFormContentType)
        {
            throw new ExcepcionValidacion(
                "archivo",
                $"El archivo debe enviarse como formulario multiparte, en el campo '{CampoArchivo}'.");
        }

        var formulario = await peticion.ReadFormAsync(cancelacion).ConfigureAwait(false);
        var archivo = formulario.Files[CampoArchivo] ?? formulario.Files.FirstOrDefault();

        if (archivo is null || archivo.Length == 0)
        {
            throw new ExcepcionValidacion("archivo", "No se ha recibido ningún archivo.");
        }

        // El tamaño se comprueba aquí y no solo en el manejador para cortar cuanto
        // antes; el límite real lo vuelve a aplicar la capa de aplicación sobre el
        // flujo, que es lo único en lo que se puede confiar.
        if (archivo.Length > limites.TamanoMaximoBytes)
        {
            throw new ExcepcionValidacion(
                "archivo",
                $"El archivo no puede superar {limites.TamanoMaximoBytes / 1024 / 1024} MiB.");
        }

        // ASP.NET Core ya ha volcado a disco los ficheros grandes, así que este flujo
        // admite búsqueda y no consume memoria proporcional al tamaño.
        await using var contenido = archivo.OpenReadStream();

        // Solo se usa si el contenido resulta ser audio (ver ManejadorSubirAdjunto); si
        // no llega o no es un número, se sube igual y sin duración conocida.
        long? duracionMs = formulario.TryGetValue(CampoDuracionMs, out var valorDuracion)
            && long.TryParse(valorDuracion, out var duracionParseada)
                ? duracionParseada
                : null;

        var comando = new ComandoSubirAdjunto(
            principal.ObtenerUsuarioId(),
            salaId,
            archivo.FileName,
            contenido,
            archivo.Length,
            duracionMs);

        var adjunto = await despachador.EjecutarAsync(comando, cancelacion).ConfigureAwait(false);

        return Results.Created($"/api/adjuntos/{adjunto.Id}", adjunto);
    }

    /// <summary>Devuelve el contenido descifrado de un archivo.</summary>
    /// <param name="adjuntoId">Adjunto solicitado.</param>
    /// <param name="contexto">Contexto HTTP, sobre el que se escribe la respuesta.</param>
    /// <param name="principal">Identidad de la petición.</param>
    /// <param name="despachador">Despachador CQRS.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    private static async Task DescargarAsync(
        Guid adjuntoId,
        HttpContext contexto,
        ClaimsPrincipal principal,
        IDespachador despachador,
        CancellationToken cancelacion)
    {
        var consulta = new ConsultaDescargarAdjunto(
            adjuntoId,
            principal.ObtenerUsuarioId(),
            // Un administrador puede auditar cualquier sala sin ser miembro.
            principal.EsAdministrador());

        await using var contenido = await despachador
            .ConsultarAsync(consulta, cancelacion)
            .ConfigureAwait(false);

        var respuesta = contexto.Response;
        respuesta.ContentType = contenido.TipoMime;
        respuesta.ContentLength = contenido.TamanoBytes;

        // Se fuerza la descarga en lugar de dejar que el navegador la interprete. La
        // cabecera de seguridad global ya impide adivinar el tipo, así que un archivo
        // manipulado nunca se ejecuta en el contexto del servidor.
        respuesta.Headers.ContentDisposition = new ContentDispositionHeaderValue(DispositionTypeNames.Attachment)
        {
            FileNameStar = contenido.NombreArchivo
        }.ToString();

        // Nada de caché compartida: el contenido es privado de la conversación y la
        // autorización se comprueba en cada petición.
        respuesta.Headers.CacheControl = "private, no-store";

        // El descifrado va a la respuesta según se lee del almacén de objetos: el
        // servidor no llega a tener el archivo entero en memoria en ningún momento.
        await contenido.Contenido.CopyToAsync(respuesta.Body, cancelacion).ConfigureAwait(false);
    }
}
