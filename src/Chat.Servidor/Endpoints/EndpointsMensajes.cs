using System.Security.Claims;
using Chat.Aplicacion.Comandos.Mensajes;
using Chat.Aplicacion.Consultas.Mensajes;
using Chat.Aplicacion.Cqrs;
using Chat.Aplicacion.Dtos;
using Chat.Servidor.Configuracion;

namespace Chat.Servidor.Endpoints;

/// <summary>Endpoints de historial y publicación de mensajes.</summary>
public static class EndpointsMensajes
{
    /// <summary>Registra el grupo <c>/api/mensajes</c>.</summary>
    /// <param name="rutas">Constructor de rutas.</param>
    /// <returns>El mismo constructor, para encadenar llamadas.</returns>
    public static IEndpointRouteBuilder MapearEndpointsMensajes(this IEndpointRouteBuilder rutas)
    {
        ArgumentNullException.ThrowIfNull(rutas);

        var grupo = rutas.MapGroup("/api/mensajes")
            .WithTags("Mensajes")
            .RequireAuthorization(ExtensionesAutenticacion.PoliticaUsuarioAutenticado)
            .RequireRateLimiting(ExtensionesLimitacionPeticiones.PoliticaApi);

        grupo.MapGet("/", ObtenerAsync)
            .WithName("ObtenerMensajes")
            .WithSummary("Devuelve el historial reciente de una sala, ya descifrado.");

        grupo.MapPost("/", EnviarAsync)
            .WithName("EnviarMensaje")
            .WithSummary("Publica un mensaje en una sala (alternativa HTTP al hub de SignalR).");

        return rutas;
    }

    /// <summary>Devuelve el historial de una sala.</summary>
    /// <param name="salaId">Sala consultada.</param>
    /// <param name="principal">Identidad de la petición.</param>
    /// <param name="despachador">Despachador CQRS.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    /// <param name="cantidad">Número máximo de mensajes.</param>
    /// <param name="anteriorA">Paginación hacia atrás por fecha.</param>
    private static async Task<IResult> ObtenerAsync(
        Guid salaId,
        ClaimsPrincipal principal,
        IDespachador despachador,
        CancellationToken cancelacion,
        int cantidad = 50,
        DateTimeOffset? anteriorA = null)
    {
        var consulta = new ConsultaObtenerMensajes(
            salaId,
            principal.ObtenerUsuarioId(),
            cantidad,
            anteriorA,
            // Un administrador puede auditar cualquier sala sin ser miembro.
            principal.EsAdministrador());

        return Results.Ok(await despachador.ConsultarAsync(consulta, cancelacion).ConfigureAwait(false));
    }

    /// <summary>Publica un mensaje sin necesidad de una conexión SignalR abierta.</summary>
    private static async Task<IResult> EnviarAsync(
        SolicitudEnviarMensajeDto solicitud,
        ClaimsPrincipal principal,
        IDespachador despachador,
        CancellationToken cancelacion)
    {
        var mensaje = await despachador
            .EjecutarAsync(new ComandoEnviarMensaje(principal.ObtenerUsuarioId(), solicitud), cancelacion)
            .ConfigureAwait(false);

        return Results.Created($"/api/mensajes/{mensaje.Id}", mensaje);
    }
}
