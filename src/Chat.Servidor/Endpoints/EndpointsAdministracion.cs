using System.Security.Claims;
using Chat.Aplicacion.Abstracciones;
using Chat.Aplicacion.Comandos.Administracion;
using Chat.Aplicacion.Comandos.Salas;
using Chat.Aplicacion.Comandos.Usuarios;
using Chat.Aplicacion.Consultas.Administracion;
using Chat.Aplicacion.Consultas.Salas;
using Chat.Aplicacion.Cqrs;
using Chat.Servidor.Configuracion;

namespace Chat.Servidor.Endpoints;

/// <summary>
/// Endpoints reservados a la consola de administración. Todos exigen el rol
/// <c>Administrador</c> mediante la política correspondiente.
/// </summary>
public static class EndpointsAdministracion
{
    /// <summary>Registra el grupo <c>/api/admin</c>.</summary>
    /// <param name="rutas">Constructor de rutas.</param>
    /// <returns>El mismo constructor, para encadenar llamadas.</returns>
    public static IEndpointRouteBuilder MapearEndpointsAdministracion(this IEndpointRouteBuilder rutas)
    {
        ArgumentNullException.ThrowIfNull(rutas);

        var grupo = rutas.MapGroup("/api/admin")
            .WithTags("Administración")
            .RequireAuthorization(ExtensionesAutenticacion.PoliticaAdministrador)
            .RequireRateLimiting(ExtensionesLimitacionPeticiones.PoliticaApi);

        grupo.MapDelete("/usuarios/{usuarioId:guid}", EliminarUsuarioAsync)
            .WithName("EliminarUsuario")
            .WithSummary("Elimina definitivamente una cuenta y todos sus datos asociados.");

        grupo.MapDelete("/salas/{salaId:guid}", EliminarSalaAsync)
            .WithName("EliminarSala")
            .WithSummary("Elimina una sala junto con su historial de mensajes.");

        grupo.MapPost("/cache/limpiar", LimpiarCacheAsync)
            .WithName("LimpiarCache")
            .WithSummary("Vacía por completo la caché de la plataforma.");

        grupo.MapGet("/estadisticas", ObtenerEstadisticasAsync)
            .WithName("ObtenerEstadisticas")
            .WithSummary("Devuelve un resumen de actividad de la plataforma.");

        grupo.MapGet("/conexiones", ObtenerConexionesAsync)
            .WithName("ObtenerConexiones")
            .WithSummary("Lista las conexiones SignalR abiertas en este momento.");

        grupo.MapGet("/salas", ListarTodasLasSalasAsync)
            .WithName("ListarTodasLasSalas")
            .WithSummary("Lista todas las salas, incluidas las privadas y las conversaciones directas.");

        grupo.MapGet("/configuracion", ObtenerConfiguracionAsync)
            .WithName("ObtenerConfiguracionPlataforma")
            .WithSummary("Devuelve la configuración pública vigente (servida desde caché).");

        return rutas;
    }

    /// <summary>Elimina una cuenta de usuario.</summary>
    private static async Task<IResult> EliminarUsuarioAsync(
        Guid usuarioId,
        ClaimsPrincipal principal,
        IDespachador despachador,
        CancellationToken cancelacion)
        => Results.Ok(await despachador
            .EjecutarAsync(new ComandoEliminarUsuario(usuarioId, principal.ObtenerUsuarioId()), cancelacion)
            .ConfigureAwait(false));

    /// <summary>Elimina una sala y su historial.</summary>
    private static async Task<IResult> EliminarSalaAsync(
        Guid salaId,
        IDespachador despachador,
        CancellationToken cancelacion)
        => Results.Ok(await despachador
            .EjecutarAsync(new ComandoEliminarSala(salaId), cancelacion)
            .ConfigureAwait(false));

    /// <summary>Vacía la caché.</summary>
    private static async Task<IResult> LimpiarCacheAsync(
        ClaimsPrincipal principal,
        IDespachador despachador,
        CancellationToken cancelacion)
        => Results.Ok(await despachador
            .EjecutarAsync(new ComandoLimpiarCache(principal.ObtenerUsuarioId()), cancelacion)
            .ConfigureAwait(false));

    /// <summary>Devuelve el resumen de actividad.</summary>
    private static async Task<IResult> ObtenerEstadisticasAsync(
        IDespachador despachador,
        CancellationToken cancelacion)
        => Results.Ok(await despachador
            .ConsultarAsync(new ConsultaEstadisticas(), cancelacion)
            .ConfigureAwait(false));

    /// <summary>Lista las conexiones activas.</summary>
    private static async Task<IResult> ObtenerConexionesAsync(
        IDespachador despachador,
        CancellationToken cancelacion)
        => Results.Ok(await despachador
            .ConsultarAsync(new ConsultaConexionesActivas(), cancelacion)
            .ConfigureAwait(false));

    /// <summary>Lista todas las salas sin filtro de visibilidad, para auditoría.</summary>
    private static async Task<IResult> ListarTodasLasSalasAsync(
        ClaimsPrincipal principal,
        IDespachador despachador,
        CancellationToken cancelacion)
        => Results.Ok(await despachador
            .ConsultarAsync(new ConsultaListarSalas(principal.ObtenerUsuarioId(), IncluirTodas: true), cancelacion)
            .ConfigureAwait(false));

    /// <summary>Devuelve la configuración pública cacheada.</summary>
    private static async Task<IResult> ObtenerConfiguracionAsync(
        IServicioConfiguracionPlataforma configuracion,
        CancellationToken cancelacion)
        => Results.Ok(await configuracion.ObtenerAsync(cancelacion).ConfigureAwait(false));
}
