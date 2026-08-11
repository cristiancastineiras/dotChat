using Chat.Dominio.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chat.Infraestructura.Persistencia.Configuraciones;

/// <summary>Configuración de la entidad <see cref="Mensaje"/>.</summary>
public sealed class ConfiguracionMensaje : IEntityTypeConfiguration<Mensaje>
{
    /// <summary>
    /// Longitud máxima del criptograma en Base64. Se calcula sobre el mensaje más largo
    /// admitido (8192 caracteres UTF-8) más nonce y etiqueta, con margen de sobra.
    /// </summary>
    private const int LongitudMaximaTextoCifrado = 16384;

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Mensaje> constructor)
    {
        ArgumentNullException.ThrowIfNull(constructor);

        constructor.ToTable("Mensajes");
        constructor.HasKey(m => m.Id);

        // Opcional: un mensaje puede ser solo una imagen, sin pie de foto.
        constructor.Property(m => m.TextoCifrado)
            .HasMaxLength(LongitudMaximaTextoCifrado);

        constructor.Property(m => m.FechaEnvio).IsRequired();

        // Relación uno a uno con el adjunto, con la clave ajena del lado del mensaje
        // porque la imagen se sube antes de que el mensaje exista. El índice único
        // que EF crea sobre la columna impide reutilizar un adjunto en otro mensaje.
        constructor.HasOne(m => m.Adjunto)
            .WithOne(a => a.Mensaje)
            .HasForeignKey<Mensaje>(m => m.AdjuntoId)
            .OnDelete(DeleteBehavior.SetNull);

        // Índice principal de lectura: historial de una sala ordenado de más nuevo a más antiguo.
        constructor.HasIndex(m => new { m.SalaId, m.FechaEnvio })
            .HasDatabaseName("IX_Mensajes_SalaId_FechaEnvio")
            .IsDescending(false, true);

        constructor.HasIndex(m => m.UsuarioId).HasDatabaseName("IX_Mensajes_UsuarioId");
    }
}
