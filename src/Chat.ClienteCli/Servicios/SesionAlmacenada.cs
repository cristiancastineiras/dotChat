namespace Chat.ClienteCli.Servicios;

/// <summary>Sesión persistida entre ejecuciones del cliente.</summary>
/// <param name="UsuarioId">Identificador del usuario autenticado.</param>
/// <param name="NombreUsuario">Nombre de usuario.</param>
/// <param name="TokenAcceso">Token JWT de acceso.</param>
/// <param name="ExpiraEn">Fecha UTC de expiración del token de acceso.</param>
/// <param name="TokenRefresco">Token de refresco vigente.</param>
/// <param name="Roles">Roles del usuario.</param>
/// <param name="UrlServidor">Servidor al que corresponde la sesión.</param>
public sealed record SesionAlmacenada(
    Guid UsuarioId,
    string NombreUsuario,
    string TokenAcceso,
    DateTimeOffset ExpiraEn,
    string TokenRefresco,
    IReadOnlyList<string> Roles,
    string UrlServidor)
{
    /// <summary>Indica si el token de acceso sigue siendo utilizable con un margen de seguridad.</summary>
    /// <param name="ahora">Instante de referencia (UTC).</param>
    public bool AccesoVigente(DateTimeOffset ahora) => ExpiraEn > ahora.AddSeconds(30);
}
