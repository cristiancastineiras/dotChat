using System.Security.Claims;
using System.Text;
using Chat.Aplicacion.Opciones;
using Chat.Dominio.Constantes;
using Chat.Infraestructura.Seguridad;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Chat.Servidor.Configuracion;

/// <summary>Configuración de autenticación JWT y de las políticas de autorización.</summary>
public static class ExtensionesAutenticacion
{
    /// <summary>Nombre de la política que exige rol de administrador.</summary>
    public const string PoliticaAdministrador = "SoloAdministradores";

    /// <summary>Nombre de la política que exige simplemente estar autenticado.</summary>
    public const string PoliticaUsuarioAutenticado = "UsuarioAutenticado";

    /// <summary>
    /// Registra el esquema JwtBearer con validación completa de emisor, audiencia,
    /// vigencia y firma, y habilita el paso del token por cadena de consulta
    /// únicamente para la negociación de SignalR (WebSocket no admite cabeceras).
    /// </summary>
    /// <param name="servicios">Colección de servicios.</param>
    /// <param name="configuracion">Configuración de la aplicación.</param>
    /// <returns>La misma colección, para encadenar llamadas.</returns>
    public static IServiceCollection AgregarAutenticacionJwt(
        this IServiceCollection servicios,
        IConfiguration configuracion)
    {
        ArgumentNullException.ThrowIfNull(servicios);
        ArgumentNullException.ThrowIfNull(configuracion);

        var jwt = configuracion.GetSection(JwtOptions.Seccion).Get<JwtOptions>()
            ?? throw new InvalidOperationException(
                $"Falta la sección de configuración '{JwtOptions.Seccion}'.");

        var signalR = configuracion.GetSection(SignalROptions.Seccion).Get<SignalROptions>() ?? new SignalROptions();

        servicios
            .AddAuthentication(opciones =>
            {
                opciones.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                opciones.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(opciones =>
            {
                // En producción el token solo viaja por HTTPS.
                opciones.RequireHttpsMetadata = true;
                opciones.SaveToken = false;
                opciones.MapInboundClaims = false;

                opciones.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Emisor,

                    ValidateAudience = true,
                    ValidAudience = jwt.Audiencia,

                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(jwt.SegundosToleranciaReloj),

                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = GeneradorTokensJwt.ConstruirClave(jwt.ClaveFirmaBase64),

                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],

                    RequireSignedTokens = true,
                    RequireExpirationTime = true,

                    NameClaimType = ClaimTypes.Name,
                    RoleClaimType = ClaimTypes.Role
                };

                opciones.Events = new JwtBearerEvents
                {
                    OnMessageReceived = contexto =>
                    {
                        // WebSocket no permite enviar la cabecera Authorization: SignalR
                        // adjunta el token en la cadena de consulta. Se acepta solo en la
                        // ruta del hub para no ampliar la superficie de ataque.
                        var token = contexto.Request.Query["access_token"];
                        var ruta = contexto.HttpContext.Request.Path;

                        if (!string.IsNullOrEmpty(token) && ruta.StartsWithSegments(signalR.RutaHub))
                        {
                            contexto.Token = token;
                        }

                        return Task.CompletedTask;
                    },
                    OnAuthenticationFailed = contexto =>
                    {
                        // Se avisa al cliente de que debe renovar, sin detallar el motivo.
                        if (contexto.Exception is SecurityTokenExpiredException)
                        {
                            contexto.Response.Headers.Append("Token-Expirado", "true");
                        }

                        return Task.CompletedTask;
                    }
                };
            });

        servicios.AddAuthorizationBuilder()
            .AddPolicy(PoliticaUsuarioAutenticado, politica => politica
                .RequireAuthenticatedUser()
                .RequireClaim(JwtRegisteredClaimNames.Sub))
            .AddPolicy(PoliticaAdministrador, politica => politica
                .RequireAuthenticatedUser()
                .RequireRole(RolesDelSistema.Administrador));

        return servicios;
    }

    /// <summary>Obtiene el identificador del usuario autenticado a partir de sus claims.</summary>
    /// <param name="principal">Identidad de la petición.</param>
    /// <returns>Identificador del usuario.</returns>
    /// <exception cref="UnauthorizedAccessException">Si el token no contiene un sujeto válido.</exception>
    public static Guid ObtenerUsuarioId(this ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        var valor = principal.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(valor, out var id)
            ? id
            : throw new UnauthorizedAccessException("El token no identifica a ningún usuario válido.");
    }

    /// <summary>Obtiene el nombre del usuario autenticado.</summary>
    /// <param name="principal">Identidad de la petición.</param>
    public static string ObtenerNombreUsuario(this ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        return principal.FindFirstValue(ClaimTypes.Name)
            ?? principal.FindFirstValue(JwtRegisteredClaimNames.UniqueName)
            ?? "(desconocido)";
    }

    /// <summary>Indica si el usuario autenticado tiene rol de administrador.</summary>
    /// <param name="principal">Identidad de la petición.</param>
    public static bool EsAdministrador(this ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);
        return principal.IsInRole(RolesDelSistema.Administrador);
    }

    /// <summary>
    /// Comprueba que la clave de firma configurada sea utilizable antes de arrancar,
    /// para fallar rápido y con un mensaje claro si falta un secreto.
    /// </summary>
    /// <param name="claveBase64">Clave en Base64.</param>
    public static void ValidarClaveFirma(string claveBase64)
    {
        var clave = GeneradorTokensJwt.ConstruirClave(claveBase64);

        if (clave.KeySize < 256)
        {
            throw new InvalidOperationException(
                $"La clave de firma JWT es demasiado corta ({clave.KeySize} bits); se requieren al menos 256.");
        }

        // Se fuerza la codificación para detectar de inmediato claves con caracteres inválidos.
        _ = Encoding.UTF8.GetByteCount(claveBase64);
    }
}
