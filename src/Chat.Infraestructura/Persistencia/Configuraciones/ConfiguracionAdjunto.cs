using Chat.Aplicacion.Validacion;
using Chat.Dominio.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chat.Infraestructura.Persistencia.Configuraciones;

/// <summary>Configuración de la entidad <see cref="Adjunto"/>.</summary>
public sealed class ConfiguracionAdjunto : IEntityTypeConfiguration<Adjunto>
{
    /// <summary>Longitud máxima del tipo MIME (<c>image/jpeg</c> y similares).</summary>
    private const int LongitudMaximaTipoMime = 128;

    /// <summary>Longitud de una huella SHA-256 en hexadecimal.</summary>
    private const int LongitudHuella = 64;

    /// <summary>Longitud máxima de la clave dentro del almacén de objetos.</summary>
    private const int LongitudMaximaClaveObjeto = 256;

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Adjunto> constructor)
    {
        ArgumentNullException.ThrowIfNull(constructor);

        constructor.ToTable("Adjuntos");
        constructor.HasKey(a => a.Id);

        constructor.Property(a => a.NombreArchivo)
            .IsRequired()
            .HasMaxLength(ValidadorEntrada.LongitudMaximaNombreArchivo);

        constructor.Property(a => a.TipoMime)
            .IsRequired()
            .HasMaxLength(LongitudMaximaTipoMime);

        constructor.Property(a => a.ClaveObjeto)
            .IsRequired()
            .HasMaxLength(LongitudMaximaClaveObjeto);

        // El tipo se persiste como entero: es estable frente a cambios de nombre
        // en el enumerado y ocupa lo mínimo.
        constructor.Property(a => a.Tipo)
            .IsRequired()
            .HasConversion<int>();

        constructor.Property(a => a.Huella)
            .IsRequired()
            .HasMaxLength(LongitudHuella)
            .IsFixedLength();

        constructor.Property(a => a.TamanoBytes).IsRequired();
        constructor.Property(a => a.FechaCreacion).IsRequired();

        // La clave del objeto es única: dos filas apuntando al mismo contenido harían
        // que borrar una dejase a la otra sin nada que descargar.
        constructor.HasIndex(a => a.ClaveObjeto)
            .IsUnique()
            .HasDatabaseName("IX_Adjuntos_ClaveObjeto");

        // Índice de la purga de huérfanos: recorre por fecha los adjuntos antiguos.
        constructor.HasIndex(a => a.FechaCreacion).HasDatabaseName("IX_Adjuntos_FechaCreacion");

        constructor.HasIndex(a => a.SalaId).HasDatabaseName("IX_Adjuntos_SalaId");

        // Si desaparece la sala o el autor, el adjunto deja de tener sentido y se va
        // con ellos. El objeto del almacén lo retira después la tarea de mantenimiento.
        constructor.HasOne(a => a.Sala)
            .WithMany()
            .HasForeignKey(a => a.SalaId)
            .OnDelete(DeleteBehavior.Cascade);

        constructor.HasOne(a => a.Usuario)
            .WithMany()
            .HasForeignKey(a => a.UsuarioId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
