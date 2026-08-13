using Chat.Dominio.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chat.Infraestructura.Persistencia.Configuraciones;

/// <summary>Configuración de la entidad <see cref="Usuario"/>.</summary>
public sealed class ConfiguracionUsuario : IEntityTypeConfiguration<Usuario>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Usuario> constructor)
    {
        ArgumentNullException.ThrowIfNull(constructor);

        constructor.ToTable("Usuarios");

        constructor.Property(u => u.FechaCreacion).IsRequired();
        constructor.Property(u => u.Activo).IsRequired().HasDefaultValue(true);

        // La foto de perfil vive en el almacén de objetos; aquí solo queda la ficha.
        constructor.Property(u => u.AvatarClaveObjeto).HasMaxLength(256);
        constructor.Property(u => u.AvatarTipoMime).HasMaxLength(128);

        // Propiedad calculada: no tiene columna propia.
        constructor.Ignore(u => u.TieneAvatar);

        // Identity ya crea un índice único sobre NormalizedUserName; este índice
        // adicional acelera los listados ordenados por fecha de alta.
        constructor.HasIndex(u => u.FechaCreacion).HasDatabaseName("IX_Usuarios_FechaCreacion");
        constructor.HasIndex(u => u.Activo).HasDatabaseName("IX_Usuarios_Activo");

        constructor.HasMany(u => u.Mensajes)
            .WithOne(m => m.Usuario)
            .HasForeignKey(m => m.UsuarioId)
            .OnDelete(DeleteBehavior.Cascade);

        constructor.HasMany(u => u.Membresias)
            .WithOne(m => m.Usuario)
            .HasForeignKey(m => m.UsuarioId)
            .OnDelete(DeleteBehavior.Cascade);

        constructor.HasMany(u => u.TokensRefresco)
            .WithOne(t => t.Usuario)
            .HasForeignKey(t => t.UsuarioId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
