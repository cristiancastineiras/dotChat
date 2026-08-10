using Chat.Dominio.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chat.Infraestructura.Persistencia.Configuraciones;

/// <summary>Configuración de la entidad <see cref="TokenRefresco"/>.</summary>
public sealed class ConfiguracionTokenRefresco : IEntityTypeConfiguration<TokenRefresco>
{
    /// <summary>Longitud de un hash SHA-256 codificado en Base64.</summary>
    private const int LongitudHashBase64 = 44;

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<TokenRefresco> constructor)
    {
        ArgumentNullException.ThrowIfNull(constructor);

        constructor.ToTable("TokensRefresco");
        constructor.HasKey(t => t.Id);

        constructor.Property(t => t.HashToken)
            .IsRequired()
            .HasMaxLength(LongitudHashBase64);

        constructor.Property(t => t.FechaCreacion).IsRequired();
        constructor.Property(t => t.FechaExpiracion).IsRequired();

        // La propiedad calculada EstaRevocado no se persiste.
        constructor.Ignore(t => t.EstaRevocado);

        // La búsqueda por hash es la operación crítica del refresco de sesión.
        constructor.HasIndex(t => t.HashToken)
            .IsUnique()
            .HasDatabaseName("IX_TokensRefresco_HashToken");

        constructor.HasIndex(t => new { t.UsuarioId, t.FechaExpiracion })
            .HasDatabaseName("IX_TokensRefresco_UsuarioId_FechaExpiracion");
    }
}
