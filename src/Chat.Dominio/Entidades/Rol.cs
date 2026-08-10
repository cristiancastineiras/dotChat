using Microsoft.AspNetCore.Identity;

namespace Chat.Dominio.Entidades;

/// <summary>
/// Rol de la plataforma. Se define un tipo propio para poder usar <see cref="Guid"/>
/// como clave primaria de forma coherente con <see cref="Usuario"/>.
/// </summary>
public class Rol : IdentityRole<Guid>
{
    /// <summary>Constructor requerido por Entity Framework Core e Identity.</summary>
    public Rol()
    {
    }

    /// <summary>Crea un rol con el nombre indicado.</summary>
    /// <param name="nombre">Nombre normalizado del rol.</param>
    public Rol(string nombre) : base(nombre)
    {
    }
}
