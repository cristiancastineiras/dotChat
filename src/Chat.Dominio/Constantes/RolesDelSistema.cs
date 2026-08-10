namespace Chat.Dominio.Constantes;

/// <summary>
/// Roles reconocidos por la plataforma. Se declaran como constantes para evitar
/// cadenas mágicas repartidas por el código y errores tipográficos en las políticas.
/// </summary>
public static class RolesDelSistema
{
    /// <summary>Rol con permisos administrativos completos.</summary>
    public const string Administrador = "Administrador";

    /// <summary>Rol de usuario estándar de la plataforma.</summary>
    public const string Usuario = "Usuario";

    /// <summary>Colección de todos los roles que deben existir en la base de datos.</summary>
    public static readonly IReadOnlyList<string> Todos = [Administrador, Usuario];
}
