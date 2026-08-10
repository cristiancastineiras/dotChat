using Chat.Dominio.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chat.Infraestructura.Persistencia.Configuraciones;

/// <summary>Configuración de la entidad <see cref="MiembroSala"/>.</summary>
public sealed class ConfiguracionMiembroSala : IEntityTypeConfiguration<MiembroSala>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<MiembroSala> constructor)
    {
        ArgumentNullException.ThrowIfNull(constructor);

        constructor.ToTable("MiembrosSala");

        // La clave compuesta impide por diseño que un usuario aparezca dos veces
        // en la misma sala, sin necesidad de comprobaciones adicionales.
        constructor.HasKey(m => new { m.SalaId, m.UsuarioId });

        constructor.Property(m => m.FechaUnion).IsRequired();

        // Marca de lectura: se compara con la fecha de los mensajes para calcular
        // los pendientes sin almacenar un contador que habría que mantener.
        constructor.Property(m => m.FechaUltimaLectura);

        constructor.HasIndex(m => m.UsuarioId).HasDatabaseName("IX_MiembrosSala_UsuarioId");
    }
}
