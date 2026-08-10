using Chat.Dominio.Entidades;

namespace Chat.Aplicacion.Abstracciones;

/// <summary>Token de acceso emitido junto con su fecha de expiración.</summary>
/// <param name="Valor">Cadena JWT firmada.</param>
/// <param name="ExpiraEn">Fecha UTC de expiración.</param>
/// <param name="Identificador">Identificador único del token (claim <c>jti</c>).</param>
public readonly record struct TokenAcceso(string Valor, DateTimeOffset ExpiraEn, Guid Identificador);

/// <summary>Emite los tokens de acceso y de refresco de la plataforma.</summary>
public interface IGeneradorTokens
{
    /// <summary>Genera un token JWT de acceso para el usuario indicado.</summary>
    /// <param name="usuario">Usuario autenticado.</param>
    /// <param name="roles">Roles del usuario, incluidos como claims.</param>
    TokenAcceso GenerarTokenAcceso(Usuario usuario, IReadOnlyCollection<string> roles);

    /// <summary>Genera un token de refresco criptográficamente aleatorio.</summary>
    /// <returns>Valor opaco en Base64Url que se entrega al cliente una sola vez.</returns>
    string GenerarTokenRefresco();

    /// <summary>Calcula el hash SHA-256 (Base64) de un token de refresco para su almacenamiento.</summary>
    /// <param name="tokenRefresco">Token en claro.</param>
    string CalcularHashRefresco(string tokenRefresco);
}
