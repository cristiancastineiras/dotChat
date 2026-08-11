using Chat.Aplicacion.Validacion;
using Chat.Dominio.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chat.Infraestructura.Persistencia.Configuraciones;

/// <summary>Configuración de la entidad <see cref="Sala"/>.</summary>
public sealed class ConfiguracionSala : IEntityTypeConfiguration<Sala>
{
    /// <summary>
    /// Longitud de la columna del nombre. Es mayor que la que admite el validador de
    /// entrada porque las conversaciones directas guardan un nombre interno generado
    /// («directa:{clave}») que el usuario nunca escribe ni ve.
    /// </summary>
    private const int LongitudMaximaNombreAlmacenado = 96;

    /// <summary>Longitud de la clave canónica de una conversación directa.</summary>
    private const int LongitudClaveDirecta = 65;

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Sala> constructor)
    {
        ArgumentNullException.ThrowIfNull(constructor);

        constructor.ToTable("Salas");
        constructor.HasKey(s => s.Id);

        // La columna lleva la intercalación insensible declarada en el contexto: con
        // ella, tanto el índice único como las comparaciones de igualdad tratan
        // «General» y «general» como el mismo nombre.
        constructor.Property(s => s.Nombre)
            .IsRequired()
            .HasMaxLength(LongitudMaximaNombreAlmacenado)
            .UseCollation(ContextoChat.IntercalacionInsensible);

        constructor.Property(s => s.Descripcion)
            .HasMaxLength(ValidadorEntrada.LongitudMaximaDescripcionSala);

        // El tipo se persiste como entero: es estable frente a cambios de nombre
        // en el enumerado y ocupa lo mínimo.
        constructor.Property(s => s.Tipo)
            .IsRequired()
            .HasConversion<int>();

        constructor.Property(s => s.ClaveDirecta)
            .HasMaxLength(LongitudClaveDirecta);

        constructor.Property(s => s.FechaCreacion).IsRequired();

        // El nombre de sala es único. La unicidad hereda la intercalación de la
        // columna, así que es insensible a mayúsculas y acentos.
        constructor.HasIndex(s => s.Nombre)
            .IsUnique()
            .HasDatabaseName("IX_Salas_Nombre");

        // Índice único parcial: garantiza una sola conversación directa por pareja
        // sin estorbar al resto de salas, que llevan la clave a nulo.
        constructor.HasIndex(s => s.ClaveDirecta)
            .IsUnique()
            .HasFilter("\"ClaveDirecta\" IS NOT NULL")
            .HasDatabaseName("IX_Salas_ClaveDirecta");

        constructor.HasIndex(s => s.FechaCreacion).HasDatabaseName("IX_Salas_FechaCreacion");

        // Ordenar el catálogo por actividad reciente es la consulta más frecuente
        // de la pantalla de salas del cliente.
        constructor.HasIndex(s => new { s.Tipo, s.FechaUltimaActividad })
            .HasDatabaseName("IX_Salas_Tipo_FechaUltimaActividad")
            .IsDescending(false, true);

        constructor.HasOne(s => s.Creador)
            .WithMany()
            .HasForeignKey(s => s.CreadorId)
            .OnDelete(DeleteBehavior.SetNull);

        constructor.HasMany(s => s.Mensajes)
            .WithOne(m => m.Sala)
            .HasForeignKey(m => m.SalaId)
            .OnDelete(DeleteBehavior.Cascade);

        constructor.HasMany(s => s.Miembros)
            .WithOne(m => m.Sala)
            .HasForeignKey(m => m.SalaId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
