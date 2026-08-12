using Chat.Dominio.Entidades;
using Chat.Infraestructura.Persistencia;
using Chat.Infraestructura.Persistencia.Repositorios;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Chat.Tests.Comun;

/// <summary>
/// Base de datos relacional efímera para las pruebas de persistencia.
/// </summary>
/// <remarks>
/// <para>
/// Se usa SQLite en memoria y no el proveedor «InMemory» de EF Core: aquel no es
/// relacional, ignora las claves ajenas y no sabe traducir las eliminaciones en bloque
/// (<c>ExecuteDelete</c>) que usan la purga de huérfanos y la de tokens caducados. Con
/// SQLite las consultas se traducen a SQL de verdad, que es lo que se quiere comprobar.
/// </para>
/// <para>
/// La conexión se mantiene abierta durante toda la vida del objeto porque una base
/// <c>:memory:</c> desaparece en cuanto se cierra la última conexión que la sostiene.
/// </para>
/// </remarks>
public sealed class BaseDatosDePrueba : IDisposable
{
    private readonly SqliteConnection _conexion;

    /// <summary>Crea la base de datos y el esquema.</summary>
    public BaseDatosDePrueba()
    {
        _conexion = new SqliteConnection("Filename=:memory:");
        _conexion.Open();

        // El modelo declara una intercalación insensible a mayúsculas que en producción
        // resuelve PostgreSQL con ICU. Aquí se registra su equivalente para que SQLite
        // pueda crear las columnas que la declaran.
        _conexion.CreateCollation(
            ContextoChat.IntercalacionInsensible,
            (izquierda, derecha) => string.Compare(izquierda, derecha, StringComparison.OrdinalIgnoreCase));

        Contexto = CrearContexto();
        Contexto.Database.EnsureCreated();
    }

    /// <summary>Contexto principal, compartido por la prueba.</summary>
    public ContextoChat Contexto { get; }

    /// <summary>Repositorio de usuarios sobre el contexto principal.</summary>
    public RepositorioUsuarios Usuarios => new(Contexto);

    /// <summary>Repositorio de salas sobre el contexto principal.</summary>
    public RepositorioSalas Salas => new(Contexto);

    /// <summary>Repositorio de mensajes sobre el contexto principal.</summary>
    public RepositorioMensajes Mensajes => new(Contexto);

    /// <summary>Repositorio de adjuntos sobre el contexto principal.</summary>
    public RepositorioAdjuntos Adjuntos => new(Contexto);

    /// <summary>Repositorio de tokens de refresco sobre el contexto principal.</summary>
    public RepositorioTokensRefresco TokensRefresco => new(Contexto);

    /// <summary>Unidad de trabajo sobre el contexto principal.</summary>
    public UnidadDeTrabajo UnidadDeTrabajo => new(Contexto);

    /// <summary>
    /// Abre un contexto nuevo contra la misma base de datos. Sirve para comprobar que
    /// algo se ha persistido de verdad y no solo quedó en el rastreador de cambios.
    /// </summary>
    public ContextoChat CrearContexto()
        => new(new DbContextOptionsBuilder<ContextoChat>()
            .UseSqlite(_conexion)
            .ReplaceService<IModelCustomizer, PersonalizadorSqlite>()
            .Options);

    /// <summary>Inserta entidades y confirma los cambios.</summary>
    /// <param name="entidades">Entidades a persistir.</param>
    public async Task SembrarAsync(params object[] entidades)
    {
        await Contexto.AddRangeAsync(entidades).ConfigureAwait(false);
        await Contexto.SaveChangesAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Da de alta un usuario rellenando los campos que Identity mantiene normalizados,
    /// de los que depende la búsqueda por nombre.
    /// </summary>
    /// <param name="nombre">Nombre de usuario.</param>
    /// <param name="activo">Indica si la cuenta está habilitada.</param>
    public async Task<Usuario> SembrarUsuarioAsync(string nombre, bool activo = true)
    {
        var usuario = Datos.Usuario(nombre: nombre, activo: activo);
        await SembrarAsync(usuario).ConfigureAwait(false);
        return usuario;
    }

    /// <summary>Vacía el rastreador de cambios para forzar una lectura real en la siguiente consulta.</summary>
    public void Olvidar() => Contexto.ChangeTracker.Clear();

    /// <inheritdoc />
    public void Dispose()
    {
        Contexto.Dispose();
        _conexion.Dispose();
    }

    /// <summary>
    /// Ajusta el modelo para que SQLite pueda ejecutarlo, sin tocar el de producción.
    /// </summary>
    /// <remarks>
    /// SQLite no sabe ordenar ni comparar <see cref="DateTimeOffset"/>, que PostgreSQL
    /// almacena como <c>timestamptz</c> nativo. Se convierten todas esas columnas a un
    /// entero de tics en UTC: el orden resultante es exactamente el mismo, así que las
    /// consultas que ordenan por fecha se pueden comprobar tal cual están escritas.
    /// </remarks>
    /// <param name="dependencias">Dependencias que inyecta EF Core.</param>
    private sealed class PersonalizadorSqlite(ModelCustomizerDependencies dependencias)
        : RelationalModelCustomizer(dependencias)
    {
        private static readonly ValueConverter<DateTimeOffset, long> AFecha =
            new(valor => valor.UtcTicks, valor => new DateTimeOffset(valor, TimeSpan.Zero));

        private static readonly ValueConverter<DateTimeOffset?, long?> AFechaOpcional =
            new(
                valor => valor.HasValue ? valor.Value.UtcTicks : null,
                valor => valor.HasValue ? new DateTimeOffset(valor.Value, TimeSpan.Zero) : null);

        /// <inheritdoc />
        public override void Customize(ModelBuilder constructor, DbContext contexto)
        {
            base.Customize(constructor, contexto);

            ArgumentNullException.ThrowIfNull(constructor);

            foreach (var entidad in constructor.Model.GetEntityTypes())
            {
                foreach (var propiedad in entidad.GetProperties())
                {
                    if (propiedad.ClrType == typeof(DateTimeOffset))
                    {
                        propiedad.SetValueConverter(AFecha);
                    }
                    else if (propiedad.ClrType == typeof(DateTimeOffset?))
                    {
                        propiedad.SetValueConverter(AFechaOpcional);
                    }
                }
            }
        }
    }
}
