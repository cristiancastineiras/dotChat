using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Chat.Aplicacion.Abstracciones;
using Chat.Aplicacion.Opciones;
using Chat.Dominio.Entidades;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Chat.Infraestructura.Seguridad;

/// <summary>
/// Emite tokens JWT firmados con HMAC-SHA256 y tokens de refresco aleatorios.
/// La clave de firma procede de la configuración y se valida al arrancar.
/// </summary>
public sealed class GeneradorTokensJwt : IGeneradorTokens
{
    /// <summary>Bytes de entropía de un token de refresco (256 bits).</summary>
    private const int BytesTokenRefresco = 32;

    /// <summary>Longitud mínima admitida para la clave de firma (256 bits).</summary>
    private const int BytesMinimosClaveFirma = 32;

    private readonly JwtOptions _opciones;
    private readonly SigningCredentials _credenciales;
    private readonly IProveedorFechaHora _reloj;
    private readonly JsonWebTokenHandler _manejador = new();

    /// <summary>Crea el generador.</summary>
    /// <param name="opciones">Opciones JWT.</param>
    /// <param name="reloj">Proveedor de fecha y hora.</param>
    public GeneradorTokensJwt(IOptions<JwtOptions> opciones, IProveedorFechaHora reloj)
    {
        ArgumentNullException.ThrowIfNull(opciones);

        _opciones = opciones.Value;
        _reloj = reloj;
        _credenciales = new SigningCredentials(
            ConstruirClave(_opciones.ClaveFirmaBase64),
            SecurityAlgorithms.HmacSha256);
    }

    /// <inheritdoc />
    public TokenAcceso GenerarTokenAcceso(Usuario usuario, IReadOnlyCollection<string> roles)
    {
        ArgumentNullException.ThrowIfNull(usuario);
        ArgumentNullException.ThrowIfNull(roles);

        var emitidoEn = _reloj.Ahora;
        var expiraEn = emitidoEn.AddMinutes(_opciones.MinutosVigenciaAcceso);
        var identificador = Guid.CreateVersion7();

        var identidad = new ClaimsIdentity(
        [
            new Claim(JwtRegisteredClaimNames.Sub, usuario.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, identificador.ToString()),
            new Claim(JwtRegisteredClaimNames.UniqueName, usuario.UserName ?? string.Empty),
            new Claim(JwtRegisteredClaimNames.Email, usuario.Email ?? string.Empty),
            new Claim(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
            new Claim(ClaimTypes.Name, usuario.UserName ?? string.Empty)
        ]);

        foreach (var rol in roles)
        {
            identidad.AddClaim(new Claim(ClaimTypes.Role, rol));
        }

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _opciones.Emisor,
            Audience = _opciones.Audiencia,
            Subject = identidad,
            IssuedAt = emitidoEn.UtcDateTime,
            NotBefore = emitidoEn.UtcDateTime,
            Expires = expiraEn.UtcDateTime,
            SigningCredentials = _credenciales
        };

        return new TokenAcceso(_manejador.CreateToken(descriptor), expiraEn, identificador);
    }

    /// <inheritdoc />
    public string GenerarTokenRefresco()
        => Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(BytesTokenRefresco));

    /// <inheritdoc />
    public string CalcularHashRefresco(string tokenRefresco)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenRefresco);

        // SHA-256 sin sal es suficiente: el token tiene 256 bits de entropía real,
        // por lo que no es vulnerable a diccionario ni a tablas precalculadas.
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(tokenRefresco));
        return Convert.ToBase64String(hash);
    }

    /// <summary>Decodifica y valida la clave simétrica de firma.</summary>
    /// <param name="claveBase64">Clave en Base64 procedente de la configuración.</param>
    public static SymmetricSecurityKey ConstruirClave(string claveBase64)
    {
        if (string.IsNullOrWhiteSpace(claveBase64))
        {
            throw new InvalidOperationException(
                "No se ha configurado la clave de firma JWT ('Jwt:ClaveFirmaBase64'). " +
                "Defínala mediante «dotnet user-secrets» o una variable de entorno; nunca en el código fuente.");
        }

        byte[] clave;
        try
        {
            clave = Convert.FromBase64String(claveBase64);
        }
        catch (FormatException excepcion)
        {
            throw new InvalidOperationException("La clave de firma JWT no es Base64 válido.", excepcion);
        }

        if (clave.Length < BytesMinimosClaveFirma)
        {
            throw new InvalidOperationException(
                $"La clave de firma JWT debe medir al menos {BytesMinimosClaveFirma} bytes (256 bits); " +
                $"la configurada mide {clave.Length}.");
        }

        return new SymmetricSecurityKey(clave);
    }

    /// <summary>Genera una clave de firma aleatoria en Base64 para aprovisionamiento.</summary>
    public static string GenerarClaveFirmaBase64()
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(BytesMinimosClaveFirma));
}
