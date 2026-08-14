using Chat.Aplicacion.Abstracciones;
using Chat.Aplicacion.Opciones;
using Chat.Dominio.Abstracciones;
using Chat.Dominio.Entidades;
using Chat.Infraestructura.Almacenamiento;
using Chat.Infraestructura.Audio;
using Chat.Infraestructura.Cache;
using Chat.Infraestructura.Identidad;
using Chat.Infraestructura.Imagenes;
using Chat.Infraestructura.Persistencia;
using Chat.Infraestructura.Persistencia.Repositorios;
using Chat.Infraestructura.Seguridad;
using Chat.Infraestructura.Tiempo;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Chat.Infraestructura;

/// <summary>Registro de la capa de infraestructura en el contenedor de dependencias.</summary>
public static class ExtensionesInyeccionDependencias
{
    /// <summary>Nombre de la cadena de conexión en la configuración.</summary>
    public const string NombreCadenaConexion = "BaseDatos";

    /// <summary>
    /// Registra persistencia, Identity, seguridad, almacén de objetos y todo lo que se
    /// apoya en Valkey.
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
        servicios.AgregarAlmacenObjetos();
        servicios.AgregarValkey(configuracion);

        return servicios;
    }

    /// <summary>Registra el almacén de objetos donde viven los archivos adjuntos.</summary>
    /// <param name="servicios">Colección de servicios.</param>
    public static IServiceCollection AgregarAlmacenObjetos(this IServiceCollection servicios)
    {
        // El cliente de S3 mantiene su propio grupo de conexiones y es seguro para uso
        // concurrente: una sola instancia sirve a todo el proceso.
        servicios.AddSingleton<IAlmacenObjetos, AlmacenObjetosS3>();

        return servicios;
    }

    /// <summary>Registra el contexto de EF Core, los repositorios y la unidad de trabajo.</summary>
    /// <param name="servicios">Colección de servicios.</param>
    /// <param name="configuracion">Configuración de la aplicación.</param>
    /// <exception cref="InvalidOperationException">Si no se ha configurado la cadena de conexión.</exception>
    public static IServiceCollection AgregarPersistencia(
        this IServiceCollection servicios,
        IConfiguration configuracion)
    {
        var cadena = configuracion.GetConnectionString(NombreCadenaConexion);

        if (string.IsNullOrWhiteSpace(cadena))
        {
            throw new InvalidOperationException(
                $"No se ha configurado la cadena de conexión '{NombreCadenaConexion}'. " +
                "Defínala en appsettings.json o en la variable de entorno " +
                "'DOTCHAT_ConnectionStrings__BaseDatos'.");
        }

        servicios.AddDbContext<ContextoChat>(opciones =>
            opciones.UseNpgsql(cadena, postgres =>
            {
                postgres.MigrationsAssembly(typeof(ContextoChat).Assembly.FullName);
                postgres.CommandTimeout(30);

                // Una caída breve de la red o un reinicio del servidor no deben
                // propagarse como error al usuario: la estrategia reintenta los
                // fallos que Npgsql clasifica como transitorios.
                postgres.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(2),
                    errorCodesToAdd: null);
            }));

        servicios.AddScoped<IUnidadDeTrabajo, UnidadDeTrabajo>();
        servicios.AddScoped<IRepositorioUsuarios, RepositorioUsuarios>();
        servicios.AddScoped<IRepositorioSalas, RepositorioSalas>();
        servicios.AddScoped<IRepositorioMensajes, RepositorioMensajes>();
        servicios.AddScoped<IRepositorioAdjuntos, RepositorioAdjuntos>();
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
        // AesGcm es seguro para uso concurrente. La misma instancia sirve las dos
        // caras del cifrado —búferes completos para el texto y flujos para los
        // archivos—, de modo que hay una única clave y un único sitio donde cargarla.
        servicios.AddSingleton<ServicioCifradorMensajes>(proveedor =>
            new ServicioCifradorMensajes(proveedor.GetRequiredService<IOptions<CifradoOptions>>()));

        servicios.AddSingleton<ICifradorMensajes>(
            proveedor => proveedor.GetRequiredService<ServicioCifradorMensajes>());

        servicios.AddSingleton<ICifradorFlujo>(
            proveedor => proveedor.GetRequiredService<ServicioCifradorMensajes>());

        servicios.AddSingleton<IGeneradorTokens, GeneradorTokensJwt>();
        servicios.AddSingleton<IProtectorRepeticion, ProtectorRepeticion>();

        // Sin estado propio más allá de las opciones: una sola instancia sirve a
        // todas las subidas concurrentes.
        servicios.AddSingleton<IProcesadorImagenes, ProcesadorImagenesImageSharp>();

        // Sin estado en absoluto: solo compara bytes.
        servicios.AddSingleton<IProcesadorAudio, ProcesadorAudioSniffer>();

        return servicios;
    }
}
