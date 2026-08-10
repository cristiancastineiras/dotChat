using Chat.Dominio.Entidades;
using Chat.Infraestructura.Persistencia.Convertidores;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Chat.Infraestructura.Persistencia;

/// <summary>
/// Contexto de Entity Framework Core de la plataforma. Extiende el contexto de
/// ASP.NET Core Identity para reutilizar sus tablas de usuarios, roles y claims.
/// </summary>
public class ContextoChat : IdentityDbContext<Usuario, Rol, Guid>
{
    /// <summary>Crea el contexto con las opciones indicadas.</summary>
    /// <param name="opciones">Opciones de configuración del contexto.</param>
    public ContextoChat(DbContextOptions<ContextoChat> opciones) : base(opciones)
    {
    }

    /// <summary>Salas de conversación.</summary>
    public DbSet<Sala> Salas => Set<Sala>();

    /// <summary>Mensajes cifrados.</summary>
    public DbSet<Mensaje> Mensajes => Set<Mensaje>();

    /// <summary>Membresías de sala.</summary>
    public DbSet<MiembroSala> MiembrosSala => Set<MiembroSala>();

    /// <summary>Tokens de refresco emitidos.</summary>
    public DbSet<TokenRefresco> TokensRefresco => Set<TokenRefresco>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder constructor)
    {
        base.OnModelCreating(constructor);

        // Todas las configuraciones de entidad viven en clases IEntityTypeConfiguration
        // dentro de este mismo ensamblado.
        constructor.ApplyConfigurationsFromAssembly(typeof(ContextoChat).Assembly);
    }

    /// <inheritdoc />
    protected override void ConfigureConventions(ModelConfigurationBuilder constructor)
    {
        ArgumentNullException.ThrowIfNull(constructor);

        base.ConfigureConventions(constructor);

        // Convención global: todas las marcas de tiempo se persisten como enteros
        // para que SQLite pueda compararlas y ordenarlas usando los índices.
        constructor.Properties<DateTimeOffset>().HaveConversion<ConvertidorFechaHora>();
        constructor.Properties<DateTimeOffset?>().HaveConversion<ConvertidorFechaHoraOpcional>();
    }
}
