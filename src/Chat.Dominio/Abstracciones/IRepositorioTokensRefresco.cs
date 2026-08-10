using Chat.Dominio.Entidades;

namespace Chat.Dominio.Abstracciones;

/// <summary>Acceso a datos de la entidad <see cref="TokenRefresco"/>.</summary>
public interface IRepositorioTokensRefresco
{
    /// <summary>Añade un token de refresco nuevo.</summary>
    /// <param name="token">Token a registrar.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    Task AgregarAsync(TokenRefresco token, CancellationToken cancelacion = default);

    /// <summary>Busca un token por su hash.</summary>
    /// <param name="hash">Hash SHA-256 en Base64 del token entregado al cliente.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    Task<TokenRefresco?> ObtenerPorHashAsync(string hash, CancellationToken cancelacion = default);

    /// <summary>Revoca todos los tokens activos de un usuario (cierre de sesión global).</summary>
    /// <param name="usuarioId">Identificador del usuario.</param>
    /// <param name="ahora">Instante de revocación (UTC).</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    Task RevocarTodosAsync(Guid usuarioId, DateTimeOffset ahora, CancellationToken cancelacion = default);

    /// <summary>Elimina los tokens caducados o revocados anteriores a la fecha indicada.</summary>
    /// <param name="limite">Fecha límite (UTC).</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    /// <returns>Número de tokens eliminados.</returns>
    Task<int> PurgarAsync(DateTimeOffset limite, CancellationToken cancelacion = default);
}
