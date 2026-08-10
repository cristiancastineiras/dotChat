using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Chat.Infraestructura.Persistencia.Convertidores;

/// <summary>
/// Convierte <see cref="DateTimeOffset"/> a un entero de 64 bits con los «ticks» UTC.
/// </summary>
/// <remarks>
/// El proveedor de SQLite guarda <see cref="DateTimeOffset"/> como texto y no puede
/// traducir comparaciones ni ordenaciones sobre esas columnas. Al almacenarlas como
/// enteros, los filtros por rango y los <c>ORDER BY</c> se ejecutan en la base de datos
/// y aprovechan los índices, en lugar de forzar evaluación en cliente.
/// Todas las fechas de la plataforma son UTC, por lo que la conversión no pierde información.
/// </remarks>
public sealed class ConvertidorFechaHora : ValueConverter<DateTimeOffset, long>
{
    /// <summary>Crea el convertidor.</summary>
    public ConvertidorFechaHora()
        : base(
            fecha => fecha.UtcTicks,
            ticks => new DateTimeOffset(ticks, TimeSpan.Zero))
    {
    }
}

/// <summary>Variante de <see cref="ConvertidorFechaHora"/> para columnas opcionales.</summary>
public sealed class ConvertidorFechaHoraOpcional : ValueConverter<DateTimeOffset?, long?>
{
    /// <summary>Crea el convertidor.</summary>
    public ConvertidorFechaHoraOpcional()
        : base(
            fecha => fecha == null ? null : fecha.Value.UtcTicks,
            ticks => ticks == null ? null : new DateTimeOffset(ticks.Value, TimeSpan.Zero))
    {
    }
}
