using Chat.Dominio.Entidades;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Chat.Infraestructura.Persistencia;

/// <summary>
/// Contexto de Entity Framework Core de la plataforma. Extiende el contexto de
/// ASP.NET Core Identity para reutilizar sus tablas de usuarios, roles y claims.
/// </summary>
public class ContextoChat : IdentityDbContext<Usuario, Rol, Guid>
{
    /// <summary>
    /// Nombre de la intercalación insensible a mayúsculas y acentos usada en las
    /// columnas cuyo valor identifica algo ante el usuario (el nombre de una sala).
    /// </summary>
    public const string IntercalacionInsensible = "insensible_mayusculas";

    /// <summary>Crea el contexto con las opciones indicadas.</summary>
    /// <param name="opciones">Opciones de configuración del contexto.</param>
    public ContextoChat(DbContextOptions<ContextoChat> opciones) : base(opciones)
    {
    }

    /// <summary>Salas de conversación.</summary>
    public DbSet<Sala> Salas => Set<Sala>();

    /// <summary>Mensajes cifrados.</summary>
    public DbSet<Mensaje> Mensajes => Set<Mensaje>();

    /// <summary>Fichas de los archivos adjuntos; el contenido vive en el almacén de objetos.</summary>
    public DbSet<Adjunto> Adjuntos => Set<Adjunto>();

    /// <summary>Membresías de sala.</summary>
    public DbSet<MiembroSala> MiembrosSala => Set<MiembroSala>();

    /// <summary>Tokens de refresco emitidos.</summary>
    public DbSet<TokenRefresco> TokensRefresco => Set<TokenRefresco>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder constructor)
    {
        ArgumentNullException.ThrowIfNull(constructor);

        base.OnModelCreating(constructor);

        // Intercalación ICU no determinista: PostgreSQL compara ignorando mayúsculas
        // y acentos, de modo que la unicidad del nombre de sala impide a la vez
        // «General», «general» y «Genéral» sin necesidad de columnas normalizadas
        // aparte ni de funciones en los índices.
        constructor.HasCollation(
            IntercalacionInsensible,
            locale: "und-u-ks-level1",
            provider: "icu",
            deterministic: false);

        // Todas las configuraciones de entidad viven en clases IEntityTypeConfiguration
        // dentro de este mismo ensamblado.
        constructor.ApplyConfigurationsFromAssembly(typeof(ContextoChat).Assembly);
    }
}
