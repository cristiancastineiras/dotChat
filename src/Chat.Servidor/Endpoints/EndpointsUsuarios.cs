using System.Security.Claims;
using Chat.Aplicacion.Consultas.Usuarios;
using Chat.Aplicacion.Cqrs;
using Chat.Servidor.Configuracion;

namespace Chat.Servidor.Endpoints;

/// <summary>Endpoints de consulta de usuarios.</summary>
public static class EndpointsUsuarios
{
    /// <summary>Registra el grupo <c>/api/usuarios</c>.</summary>
    /// <param name="rutas">Constructor de rutas.</param>
    /// <returns>El mismo constructor, para encadenar llamadas.</returns>
    public static IEndpointRouteBuilder MapearEndpointsUsuarios(this IEndpointRouteBuilder rutas)
    {
        ArgumentNullException.ThrowIfNull(rutas);

        var grupo = rutas.MapGroup("/api/usuarios")
            .WithTags("Usuarios")
            .RequireAuthorization(ExtensionesAutenticacion.PoliticaUsuarioAutenticado)
            .RequireRateLimiting(ExtensionesLimitacionPeticiones.PoliticaApi);

        grupo.MapGet("/", ListarAsync)
            .WithName("ListarUsuarios")
            .WithSummary("Lista los usuarios de la plataforma.");

        grupo.MapGet("/presencia", ObtenerPresenciaAsync)
            .WithName("ObtenerPresencia")
            .WithSummary("Devuelve quién está en línea y cuándo se vio por última vez al resto.");

        grupo.MapGet("/yo", ObtenerPropio)
            .WithName("ObtenerUsuarioActual")
            .WithSummary("Devuelve la identidad asociada al token presentado.");

        return rutas;
    }

    /// <summary>Devuelve el estado de conexión de los usuarios conocidos.</summary>
    private static async Task<IResult> ObtenerPresenciaAsync(
        IDespachador despachador,
        CancellationToken cancelacion)
        => Results.Ok(await despachador
            .ConsultarAsync(new ConsultaPresencia(), cancelacion)
            .ConfigureAwait(false));

    /// <summary>Lista los usuarios; solo los administradores ven las cuentas desactivadas.</summary>
    private static async Task<IResult> ListarAsync(
        ClaimsPrincipal principal,
        IDespachador despachador,
        CancellationToken cancelacion,
        bool incluirInactivos = false)
    {
        var incluir = incluirInactivos && principal.EsAdministrador();

        var usuarios = await despachador
            .ConsultarAsync(new ConsultaListarUsuarios(incluir), cancelacion)
            .ConfigureAwait(false);

        return Results.Ok(usuarios);
    }

    /// <summary>Devuelve los datos básicos del usuario autenticado tomados del token.</summary>
    private static IResult ObtenerPropio(ClaimsPrincipal principal) => Results.Ok(new
    {
        id = principal.ObtenerUsuarioId(),
        nombreUsuario = principal.ObtenerNombreUsuario(),
        esAdministrador = principal.EsAdministrador()
    });
}
