using Chat.Aplicacion.Abstracciones;

namespace Chat.Infraestructura.Tiempo;

/// <summary>Proveedor de fecha y hora respaldado por el reloj del sistema, siempre en UTC.</summary>
public sealed class ProveedorFechaHoraSistema : IProveedorFechaHora
{
    private readonly TimeProvider _proveedor;

    /// <summary>Crea el proveedor usando el reloj del sistema.</summary>
    public ProveedorFechaHoraSistema() : this(TimeProvider.System)
    {
    }

    /// <summary>Crea el proveedor sobre un <see cref="TimeProvider"/> concreto (útil en pruebas).</summary>
    /// <param name="proveedor">Origen del tiempo.</param>
    public ProveedorFechaHoraSistema(TimeProvider proveedor) => _proveedor = proveedor;

    /// <inheritdoc />
    public DateTimeOffset Ahora => _proveedor.GetUtcNow();
}
