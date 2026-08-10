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
    /// <inheritdoc />
    public ContextoChat CreateDbContext(string[] args)
    {
        var cadena = args is { Length: > 0 } && !string.IsNullOrWhiteSpace(args[0])
            ? args[0]
            : "Data Source=chat-diseno.db";

        var opciones = new DbContextOptionsBuilder<ContextoChat>()
            .UseSqlite(cadena, sqlite => sqlite.MigrationsAssembly(typeof(ContextoChat).Assembly.FullName))
            .Options;

        return new ContextoChat(opciones);
    }
}
