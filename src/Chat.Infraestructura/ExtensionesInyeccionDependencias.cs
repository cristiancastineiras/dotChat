using Chat.Aplicacion.Abstracciones;
using Chat.Aplicacion.Opciones;
using Chat.Dominio.Abstracciones;
using Chat.Dominio.Entidades;
using Chat.Infraestructura.Cache;
using Chat.Infraestructura.Identidad;
using Chat.Infraestructura.Persistencia;
using Chat.Infraestructura.Persistencia.Repositorios;
using Chat.Infraestructura.Seguridad;
using Chat.Infraestructura.Tiempo;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ZiggyCreatures.Caching.Fusion;

namespace Chat.Infraestructura;

/// <summary>Registro de la capa de infraestructura en el contenedor de dependencias.</summary>
public static class ExtensionesInyeccionDependencias
{
    /// <summary>Nombre de la cadena de conexión en la configuración.</summary>
    public const string NombreCadenaConexion = "BaseDatos";

    /// <summary>
    /// Registra persistencia, Identity, seguridad y caché.
    /// </summary>
    /// <param name="servicios">Colección de servicios.</param>
    /// <param name="configuracion">Configuración de la aplicación.</param>
    /// <returns>La misma colección, para encadenar llamadas.</returns>
    public static IServiceCollection AgregarInfraestructura(
        this IServiceCollection servicios,
        IConfiguration configuracion)
    {
        ArgumentNullException.ThrowIfNull(servicios);
        ArgumentNullException.ThrowIfNull(configuracion);

        servicios.AgregarPersistencia(configuracion);
        servicios.AgregarIdentidad();
        servicios.AgregarSeguridad();
        servicios.AgregarCache();

        return servicios;
    }

    /// <summary>Registra el contexto de EF Core, los repositorios y la unidad de trabajo.</summary>
    /// <param name="servicios">Colección de servicios.</param>
    /// <param name="configuracion">Configuración de la aplicación.</param>
    public static IServiceCollection AgregarPersistencia(
        this IServiceCollection servicios,
        IConfiguration configuracion)
    {
        var cadena = configuracion.GetConnectionString(NombreCadenaConexion)
            ?? "Data Source=chat.db";

        servicios.AddDbContext<ContextoChat>(opciones =>
            opciones.UseSqlite(cadena, sqlite =>
            {
                sqlite.MigrationsAssembly(typeof(ContextoChat).Assembly.FullName);
                sqlite.CommandTimeout(30);
            }));

        servicios.AddScoped<IUnidadDeTrabajo, UnidadDeTrabajo>();
        servicios.AddScoped<IRepositorioUsuarios, RepositorioUsuarios>();
        servicios.AddScoped<IRepositorioSalas, RepositorioSalas>();
        servicios.AddScoped<IRepositorioMensajes, RepositorioMensajes>();
        servicios.AddScoped<IRepositorioTokensRefresco, RepositorioTokensRefresco>();
        servicios.AddScoped<InicializadorBaseDatos>();

        return servicios;
    }

    /// <summary>Registra ASP.NET Core Identity con políticas de seguridad estrictas.</summary>
    /// <param name="servicios">Colección de servicios.</param>
    public static IServiceCollection AgregarIdentidad(this IServiceCollection servicios)
    {
        servicios
            .AddIdentityCore<Usuario>(opciones =>
            {
                // Contraseñas: longitud generosa y variedad de caracteres obligatoria.
                opciones.Password.RequiredLength = 10;
                opciones.Password.RequireDigit = true;
                opciones.Password.RequireLowercase = true;
                opciones.Password.RequireUppercase = true;
                opciones.Password.RequireNonAlphanumeric = true;
                opciones.Password.RequiredUniqueChars = 4;

                // Bloqueo temporal ante intentos repetidos: frena la fuerza bruta.
                opciones.Lockout.AllowedForNewUsers = true;
                opciones.Lockout.MaxFailedAccessAttempts = 5;
                opciones.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);

                opciones.User.RequireUniqueEmail = true;
                opciones.User.AllowedUserNameCharacters =
                    "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._";

                opciones.SignIn.RequireConfirmedAccount = false;
            })
            .AddRoles<Rol>()
            .AddEntityFrameworkStores<ContextoChat>();

        servicios.AddScoped<IServicioIdentidad, ServicioIdentidad>();

        return servicios;
    }

    /// <summary>Registra el cifrador de mensajes, el generador de tokens y la protección antirrepetición.</summary>
    /// <param name="servicios">Colección de servicios.</param>
    public static IServiceCollection AgregarSeguridad(this IServiceCollection servicios)
    {
        servicios.AddSingleton<IProveedorFechaHora, ProveedorFechaHoraSistema>();

        // El cifrador es singleton: mantiene la clave AES cargada una sola vez y
        // AesGcm es seguro para uso concurrente.
        servicios.AddSingleton<ICifradorMensajes>(proveedor =>
            new ServicioCifradorMensajes(proveedor.GetRequiredService<IOptions<CifradoOptions>>()));

        servicios.AddSingleton<IGeneradorTokens, GeneradorTokensJwt>();
        servicios.AddSingleton<IProtectorRepeticion, ProtectorRepeticion>();

        return servicios;
    }

    /// <summary>Registra FusionCache y su adaptador.</summary>
    /// <param name="servicios">Colección de servicios.</param>
    public static IServiceCollection AgregarCache(this IServiceCollection servicios)
    {
        servicios.AddFusionCache()
            .WithOptions(opciones =>
            {
                // Un fallo de la caché nunca debe tumbar una petición.
                opciones.DefaultEntryOptions = new FusionCacheEntryOptions(TimeSpan.FromMinutes(1))
                {
                    IsFailSafeEnabled = true,
                    FailSafeMaxDuration = TimeSpan.FromHours(1)
                };
            });

        servicios.AddSingleton<IServicioCache, ServicioCacheFusion>();

        return servicios;
    }
}
