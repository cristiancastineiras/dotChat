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

    /// <summary>Mensajes enviados por el usuario.</summary>
    public ICollection<Mensaje> Mensajes { get; set; } = [];

    /// <summary>Salas a las que pertenece el usuario.</summary>
    public ICollection<MiembroSala> Membresias { get; set; } = [];

    /// <summary>Tokens de refresco emitidos para el usuario.</summary>
    public ICollection<TokenRefresco> TokensRefresco { get; set; } = [];
}
