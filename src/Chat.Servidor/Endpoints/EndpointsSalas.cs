using System.Security.Claims;
using Chat.Aplicacion.Comandos.Salas;
using Chat.Aplicacion.Consultas.Salas;
using Chat.Aplicacion.Cqrs;
using Chat.Aplicacion.Dtos;
using Chat.Servidor.Configuracion;

namespace Chat.Servidor.Endpoints;

/// <summary>Endpoints de gestión de salas y de conversaciones directas.</summary>
public static class EndpointsSalas
{
    /// <summary>Registra el grupo <c>/api/salas</c>.</summary>
    /// <param name="rutas">Constructor de rutas.</param>
    /// <returns>El mismo constructor, para encadenar llamadas.</returns>
    public static IEndpointRouteBuilder MapearEndpointsSalas(this IEndpointRouteBuilder rutas)
    {
        ArgumentNullException.ThrowIfNull(rutas);

        var grupo = rutas.MapGroup("/api/salas")
            .WithTags("Salas")
            .RequireAuthorization(ExtensionesAutenticacion.PoliticaUsuarioAutenticado)
            .RequireRateLimiting(ExtensionesLimitacionPeticiones.PoliticaApi);

        grupo.MapGet("/", ListarAsync)
            .WithName("ListarSalas")
            .WithSummary("Lista las salas visibles: las públicas y las privadas propias.");

        grupo.MapGet("/mias", ListarPropiasAsync)
            .WithName("ListarSalasPropias")
            .WithSummary("Lista las salas y conversaciones del usuario, con sus mensajes pendientes.");

        grupo.MapPost("/", CrearAsync)
            .WithName("CrearSala")
            .WithSummary("Crea una sala nueva, pública o privada, y da de alta al creador como miembro.");

        grupo.MapPost("/directas", AbrirDirectaAsync)
            .WithName("AbrirConversacionDirecta")
            .WithSummary("Abre o recupera la conversación privada con otra persona.");

        grupo.MapGet("/{salaId:guid}/miembros", ListarMiembrosAsync)
            .WithName("ListarMiembrosSala")
            .WithSummary("Lista los miembros de una sala con su estado de conexión.");

        grupo.MapPost("/{salaId:guid}/unirse", UnirseAsync)
            .WithName("UnirseSala")
            .WithSummary("Une al usuario autenticado a la sala pública indicada.");

        grupo.MapPost("/{salaId:guid}/invitar", InvitarAsync)
            .WithName("InvitarASala")
            .WithSummary("Incorpora a otro usuario a la sala; única vía de entrada a una sala privada.");

        grupo.MapPost("/{salaId:guid}/leida", MarcarLeidaAsync)
            .WithName("MarcarSalaLeida")
            .WithSummary("Pone a cero los mensajes pendientes del usuario en la sala.");

        grupo.MapPost("/{salaId:guid}/salir", SalirAsync)
            .WithName("SalirSala")
            .WithSummary("Saca al usuario autenticado de la sala indicada.");

        return rutas;
    }

    /// <summary>Lista el catálogo de salas visible para el usuario.</summary>
    private static async Task<IResult> ListarAsync(
        ClaimsPrincipal principal,
        IDespachador despachador,
        CancellationToken cancelacion)
    {
        var consulta = new ConsultaListarSalas(
            principal.ObtenerUsuarioId(),
            // Un administrador ve también las privadas ajenas y las conversaciones
            // directas, porque su consola tiene que poder auditarlas.
            principal.EsAdministrador());

        return Results.Ok(await despachador.ConsultarAsync(consulta, cancelacion).ConfigureAwait(false));
    }

    /// <summary>Lista las salas y conversaciones del usuario autenticado.</summary>
    private static async Task<IResult> ListarPropiasAsync(
        ClaimsPrincipal principal,
        IDespachador despachador,
        CancellationToken cancelacion)
        => Results.Ok(await despachador
            .ConsultarAsync(new ConsultaSalasDeUsuario(principal.ObtenerUsuarioId()), cancelacion)
            .ConfigureAwait(false));

    /// <summary>Crea una sala nueva.</summary>
    private static async Task<IResult> CrearAsync(
        SolicitudCrearSalaDto solicitud,
        ClaimsPrincipal principal,
        IDespachador despachador,
        CancellationToken cancelacion)
    {
        var sala = await despachador
            .EjecutarAsync(new ComandoCrearSala(solicitud, principal.ObtenerUsuarioId()), cancelacion)
            .ConfigureAwait(false);

        return Results.Created($"/api/salas/{sala.Id}", sala);
    }

    /// <summary>Abre o recupera la conversación directa con otro usuario.</summary>
    private static async Task<IResult> AbrirDirectaAsync(
        SolicitudConversacionDirectaDto solicitud,
        ClaimsPrincipal principal,
        IDespachador despachador,
        CancellationToken cancelacion)
    {
        var comando = new ComandoAbrirConversacionDirecta(principal.ObtenerUsuarioId(), solicitud);
        var sala = await despachador.EjecutarAsync(comando, cancelacion).ConfigureAwait(false);

        return Results.Ok(sala);
    }

    /// <summary>Lista los miembros de una sala.</summary>
    private static async Task<IResult> ListarMiembrosAsync(
        Guid salaId,
        ClaimsPrincipal principal,
        IDespachador despachador,
        CancellationToken cancelacion)
    {
        var consulta = new ConsultaMiembrosSala(
            salaId,
            principal.ObtenerUsuarioId(),
            principal.EsAdministrador());

        return Results.Ok(await despachador.ConsultarAsync(consulta, cancelacion).ConfigureAwait(false));
    }

    /// <summary>Une al usuario autenticado a una sala.</summary>
    private static async Task<IResult> UnirseAsync(
        Guid salaId,
        ClaimsPrincipal principal,
        IDespachador despachador,
        CancellationToken cancelacion)
        => Results.Ok(await despachador
            .EjecutarAsync(new ComandoUnirseSala(salaId, principal.ObtenerUsuarioId()), cancelacion)
            .ConfigureAwait(false));

    /// <summary>Incorpora a otro usuario a la sala.</summary>
    private static async Task<IResult> InvitarAsync(
        Guid salaId,
        SolicitudInvitarDto solicitud,
        ClaimsPrincipal principal,
        IDespachador despachador,
        CancellationToken cancelacion)
        => Results.Ok(await despachador
            .EjecutarAsync(new ComandoInvitarASala(salaId, principal.ObtenerUsuarioId(), solicitud), cancelacion)
            .ConfigureAwait(false));

    /// <summary>Marca la sala como leída para el usuario autenticado.</summary>
    private static async Task<IResult> MarcarLeidaAsync(
        Guid salaId,
        ClaimsPrincipal principal,
        IDespachador despachador,
        CancellationToken cancelacion)
        => Results.Ok(await despachador
            .EjecutarAsync(new ComandoMarcarSalaLeida(salaId, principal.ObtenerUsuarioId()), cancelacion)
            .ConfigureAwait(false));

    /// <summary>Saca al usuario autenticado de una sala.</summary>
    private static async Task<IResult> SalirAsync(
        Guid salaId,
        ClaimsPrincipal principal,
        IDespachador despachador,
        CancellationToken cancelacion)
        => Results.Ok(await despachador
            .EjecutarAsync(new ComandoSalirSala(salaId, principal.ObtenerUsuarioId()), cancelacion)
            .ConfigureAwait(false));
}
