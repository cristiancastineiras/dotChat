using Microsoft.AspNetCore.Identity;

namespace Chat.Dominio.Entidades;

/// <summary>
/// Usuario de la plataforma de mensajería.
/// Hereda de <see cref="IdentityUser{TKey}"/> para reutilizar el almacenamiento de
/// credenciales de ASP.NET Core Identity (hash de contraseña, sellos de seguridad,
/// bloqueo por intentos fallidos), evitando duplicar lógica de seguridad ya probada.
/// </summary>
public class Usuario : IdentityUser<Guid>
{
    /// <summary>Fecha UTC de alta del usuario.</summary>
    public DateTimeOffset FechaCreacion { get; set; }

    /// <summary>Fecha UTC del último inicio de sesión correcto; nula si nunca ha entrado.</summary>
    public DateTimeOffset? FechaUltimoAcceso { get; set; }

    /// <summary>Indica si la cuenta está activa. Las cuentas desactivadas no pueden autenticarse.</summary>
    public bool Activo { get; set; } = true;

    /// <summary>
    /// Clave del objeto que guarda la foto de perfil dentro del almacén; nula si el
    /// usuario no ha subido ninguna.
    /// </summary>
    /// <remarks>
    /// La foto sigue el mismo camino que un adjunto —se normaliza, se cifra y se
    /// guarda fuera de la base de datos—, pero no se modela como <see cref="Adjunto"/>:
    /// un adjunto pertenece a una sala y delimita ahí quién puede descargarlo, mientras
    /// que la foto de perfil es del usuario y la ve cualquiera que hable con él.
    /// </remarks>
    public string? AvatarClaveObjeto { get; set; }

    /// <summary>Tipo MIME con el que se almacenó la foto de perfil; nulo si no hay foto.</summary>
    public string? AvatarTipoMime { get; set; }

    /// <summary>
    /// Fecha UTC del último cambio de foto. Viaja a los clientes y les sirve de marca
    /// de versión: mientras no cambie, pueden seguir usando la copia que ya tienen.
    /// </summary>
    public DateTimeOffset? AvatarActualizado { get; set; }

    /// <summary>Indica si el usuario tiene una foto de perfil almacenada.</summary>
    public bool TieneAvatar => !string.IsNullOrEmpty(AvatarClaveObjeto);

    /// <summary>
    /// Construye la clave del objeto que guarda la foto. Incluye un componente
    /// aleatorio para que cada cambio escriba un objeto nuevo: así una foto sustituida
    /// nunca se sirve desde una caché intermedia que aún tuviera la clave anterior.
    /// </summary>
    /// <param name="usuarioId">Usuario propietario de la foto.</param>
    public static string ConstruirClaveAvatar(Guid usuarioId)
        => $"avatares/{usuarioId:N}/{Guid.CreateVersion7():N}";

    /// <summary>Mensajes enviados por el usuario.</summary>
    public ICollection<Mensaje> Mensajes { get; set; } = [];

    /// <summary>Salas a las que pertenece el usuario.</summary>
    public ICollection<MiembroSala> Membresias { get; set; } = [];

    /// <summary>Tokens de refresco emitidos para el usuario.</summary>
    public ICollection<TokenRefresco> TokensRefresco { get; set; } = [];
}
