using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Chat.Infraestructura.Persistencia;

/// <summary>
/// Fábrica usada exclusivamente por las herramientas de EF Core (<c>dotnet ef</c>)
/// para crear el contexto sin arrancar el servidor. La cadena de conexión que
/// emplea solo sirve para generar migraciones, nunca en ejecución.
/// </summary>
public sealed class FabricaContextoDisenio : IDesignTimeDbContextFactory<ContextoChat>
{
    /// <summary>Cadena de conexión de reserva para generar migraciones sin servidor levantado.</summary>
    private const string CadenaPorDefecto =
        "Host=localhost;Port=5432;Database=appdb;Username=appuser;Password=diseno";

    /// <inheritdoc />
    public ContextoChat CreateDbContext(string[] args)
    {
        // Se admite pasarla como argumento (`dotnet ef ... -- "Host=..."`) y, si no,
        // por variable de entorno, para no escribir credenciales reales en el código.
        var cadena = args is { Length: > 0 } && !string.IsNullOrWhiteSpace(args[0])
            ? args[0]
            : Environment.GetEnvironmentVariable("DOTCHAT_ConnectionStrings__BaseDatos") ?? CadenaPorDefecto;

        var opciones = new DbContextOptionsBuilder<ContextoChat>()
            .UseNpgsql(cadena, postgres => postgres.MigrationsAssembly(typeof(ContextoChat).Assembly.FullName))
            .Options;

        return new ContextoChat(opciones);
    }
}
