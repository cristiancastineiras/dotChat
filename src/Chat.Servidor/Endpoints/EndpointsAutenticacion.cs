using Chat.Aplicacion.Comandos.Autenticacion;
using Chat.Aplicacion.Cqrs;
using Chat.Aplicacion.Dtos;
using Chat.Servidor.Configuracion;

namespace Chat.Servidor.Endpoints;

/// <summary>Endpoints públicos de registro, inicio de sesión y renovación de sesión.</summary>
public static class EndpointsAutenticacion
{
    /// <summary>Registra el grupo <c>/api/auth</c>.</summary>
    /// <param name="rutas">Constructor de rutas.</param>
    /// <returns>El mismo constructor, para encadenar llamadas.</returns>
    public static IEndpointRouteBuilder MapearEndpointsAutenticacion(this IEndpointRouteBuilder rutas)
    {
        ArgumentNullException.ThrowIfNull(rutas);

        var grupo = rutas.MapGroup("/api/auth")
            .WithTags("Autenticación")
            .AllowAnonymous()
            // Política estricta: estos endpoints son el objetivo natural de la fuerza bruta.
            .RequireRateLimiting(ExtensionesLimitacionPeticiones.PoliticaAutenticacion);

        grupo.MapPost("/registrar", RegistrarAsync)
            .WithName("RegistrarUsuario")
            .WithSummary("Crea una cuenta nueva y devuelve la sesión iniciada.");

        grupo.MapPost("/login", IniciarSesionAsync)
            .WithName("IniciarSesion")
            .WithSummary("Autentica al usuario y devuelve un token de acceso y uno de refresco.");

        grupo.MapPost("/refrescar", RefrescarAsync)
            .WithName("RefrescarSesion")
            .WithSummary("Renueva la sesión a partir de un token de refresco válido.");

        return rutas;
    }

    /// <summary>Da de alta una cuenta nueva.</summary>
    private static async Task<IResult> RegistrarAsync(
        SolicitudRegistroDto solicitud,
        IDespachador despachador,
        CancellationToken cancelacion)
    {
        var sesion = await despachador
            .EjecutarAsync(new ComandoRegistrarUsuario(solicitud), cancelacion)
            .ConfigureAwait(false);

        return Results.Created($"/api/usuarios/{sesion.UsuarioId}", sesion);
    }

    /// <summary>Autentica al usuario.</summary>
    private static async Task<IResult> IniciarSesionAsync(
        SolicitudLoginDto solicitud,
        IDespachador despachador,
        CancellationToken cancelacion)
    {
        var sesion = await despachador
            .EjecutarAsync(new ComandoIniciarSesion(solicitud), cancelacion)
            .ConfigureAwait(false);

        return Results.Ok(sesion);
    }

    /// <summary>Renueva la sesión con rotación del token de refresco.</summary>
    private static async Task<IResult> RefrescarAsync(
        SolicitudRefrescoDto solicitud,
        IDespachador despachador,
        CancellationToken cancelacion)
    {
        var sesion = await despachador
            .EjecutarAsync(new ComandoRefrescarSesion(solicitud), cancelacion)
            .ConfigureAwait(false);

        return Results.Ok(sesion);
    }
}
