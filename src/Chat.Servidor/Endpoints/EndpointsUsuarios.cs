using System.Security.Claims;
using Chat.Aplicacion.Comandos.Usuarios;
using Chat.Aplicacion.Consultas.Usuarios;
using Chat.Aplicacion.Cqrs;
using Chat.Aplicacion.Opciones;
using Chat.Dominio.Excepciones;
using Chat.Servidor.Configuracion;
using Microsoft.Extensions.Options;

namespace Chat.Servidor.Endpoints;

/// <summary>Endpoints de consulta de usuarios y de gestión de la foto de perfil.</summary>
public static class EndpointsUsuarios
{
    /// <summary>Nombre del campo del formulario que transporta la foto.</summary>
    private const string CampoArchivo = "archivo";

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

        grupo.MapGet("/yo", ObtenerPropioAsync)
            .WithName("ObtenerUsuarioActual")
            .WithSummary("Devuelve el perfil del usuario autenticado.");

        grupo.MapPost("/yo/avatar", SubirAvatarAsync)
            .WithName("ActualizarAvatar")
            .WithSummary("Sustituye la foto de perfil del usuario autenticado.")
            .RequireRateLimiting(ExtensionesLimitacionPeticiones.PoliticaSubida)
            .DisableAntiforgery();

        grupo.MapDelete("/yo/avatar", EliminarAvatarAsync)
            .WithName("EliminarAvatar")
            .WithSummary("Retira la foto de perfil del usuario autenticado.");

        grupo.MapGet("/{usuarioId:guid}/avatar", DescargarAvatarAsync)
            .WithName("DescargarAvatar")
            .WithSummary("Devuelve la foto de perfil de un usuario, ya descifrada.")
            .RequireRateLimiting(ExtensionesLimitacionPeticiones.PoliticaDescarga);

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

    /// <summary>Devuelve el perfil del usuario autenticado.</summary>
    private static async Task<IResult> ObtenerPropioAsync(
        ClaimsPrincipal principal,
        IDespachador despachador,
        CancellationToken cancelacion)
        => Results.Ok(await despachador
            .ConsultarAsync(new ConsultaPerfil(principal.ObtenerUsuarioId()), cancelacion)
            .ConfigureAwait(false));

    /// <summary>Recibe la foto como formulario multiparte y la deja como avatar del usuario.</summary>
    /// <param name="peticion">Petición HTTP, de la que se lee el formulario.</param>
    /// <param name="principal">Identidad de la petición.</param>
    /// <param name="despachador">Despachador CQRS.</param>
    /// <param name="opciones">Límites configurados para las imágenes.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    private static async Task<IResult> SubirAvatarAsync(
        HttpRequest peticion,
        ClaimsPrincipal principal,
        IDespachador despachador,
        IOptions<AdjuntosOptions> opciones,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(peticion);

        if (!peticion.HasFormContentType)
        {
            throw new ExcepcionValidacion(
                "archivo",
                $"La foto debe enviarse como formulario multiparte, en el campo '{CampoArchivo}'.");
        }

        var formulario = await peticion.ReadFormAsync(cancelacion).ConfigureAwait(false);
        var archivo = formulario.Files[CampoArchivo] ?? formulario.Files.FirstOrDefault();

        if (archivo is null || archivo.Length == 0)
        {
            throw new ExcepcionValidacion("archivo", "No se ha recibido ninguna imagen.");
        }

        // Se corta aquí antes de descodificar nada. El límite real lo vuelve a aplicar
        // el procesador de imágenes sobre la cabecera del fichero.
        var limite = opciones.Value.TamanoMaximoImagenBytes;

        if (archivo.Length > limite)
        {
            throw new ExcepcionValidacion(
                "archivo",
                $"La foto de perfil no puede superar {limite / 1024 / 1024} MiB.");
        }

        await using var contenido = archivo.OpenReadStream();

        var perfil = await despachador
            .EjecutarAsync(new ComandoActualizarAvatar(principal.ObtenerUsuarioId(), contenido), cancelacion)
            .ConfigureAwait(false);

        return Results.Ok(perfil);
    }

    /// <summary>Retira la foto de perfil del usuario autenticado.</summary>
    private static async Task<IResult> EliminarAvatarAsync(
        ClaimsPrincipal principal,
        IDespachador despachador,
        CancellationToken cancelacion)
        => Results.Ok(await despachador
            .EjecutarAsync(new ComandoEliminarAvatar(principal.ObtenerUsuarioId()), cancelacion)
            .ConfigureAwait(false));

    /// <summary>Devuelve la foto de perfil de un usuario.</summary>
    /// <param name="usuarioId">Usuario cuya foto se pide.</param>
    /// <param name="contexto">Contexto HTTP, sobre el que se escribe la respuesta.</param>
    /// <param name="despachador">Despachador CQRS.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    private static async Task DescargarAvatarAsync(
        Guid usuarioId,
        HttpContext contexto,
        IDespachador despachador,
        CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(contexto);

        await using var contenido = await despachador
            .ConsultarAsync(new ConsultaDescargarAvatar(usuarioId), cancelacion)
            .ConfigureAwait(false);

        var respuesta = contexto.Response;
        respuesta.ContentType = contenido.TipoMime;

        // Caché privada y corta: la foto cambia poco, pero cuando cambia el cliente
        // debe verla sin tener que cerrar sesión. La marca de versión que acompaña al
        // usuario en los listados es la que decide de verdad cuándo volver a pedirla.
        respuesta.Headers.CacheControl = "private, max-age=60";

        await contenido.Contenido.CopyToAsync(respuesta.Body, cancelacion).ConfigureAwait(false);
    }
}
