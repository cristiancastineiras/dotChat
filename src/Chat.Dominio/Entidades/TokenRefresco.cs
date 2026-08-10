namespace Chat.Dominio.Entidades;

/// <summary>
/// Token de refresco emitido a un usuario. Solo se persiste su hash SHA-256,
/// de forma que una filtración de la base de datos no permita reutilizarlo.
/// </summary>
public class TokenRefresco
{
    /// <summary>Identificador único del token.</summary>
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>Usuario propietario del token.</summary>
    public Guid UsuarioId { get; set; }

    /// <summary>Usuario asociado.</summary>
    public Usuario? Usuario { get; set; }

    /// <summary>Hash SHA-256 (Base64) del token entregado al cliente.</summary>
    public required string HashToken { get; set; }

    /// <summary>Fecha UTC de emisión.</summary>
    public DateTimeOffset FechaCreacion { get; set; }

    /// <summary>Fecha UTC de expiración.</summary>
    public DateTimeOffset FechaExpiracion { get; set; }

    /// <summary>Fecha UTC de revocación; nula mientras el token siga siendo válido.</summary>
    public DateTimeOffset? FechaRevocacion { get; set; }

    /// <summary>Indica si el token está revocado.</summary>
    public bool EstaRevocado => FechaRevocacion is not null;

    /// <summary>Determina si el token sigue siendo utilizable en el instante indicado.</summary>
    /// <param name="ahora">Instante de referencia (UTC).</param>
    public bool EsValido(DateTimeOffset ahora) => !EstaRevocado && FechaExpiracion > ahora;

    /// <summary>Marca el token como revocado.</summary>
    /// <param name="ahora">Instante de revocación (UTC).</param>
    public void Revocar(DateTimeOffset ahora) => FechaRevocacion ??= ahora;
}
