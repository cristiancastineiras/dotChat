using Chat.Aplicacion.Opciones;

namespace Chat.Servidor.Configuracion;

/// <summary>
/// Registro de las opciones fuertemente tipadas (Options Pattern).
/// Todas se validan con anotaciones de datos y en el arranque, de modo que una
/// configuración incorrecta impide levantar el servidor en lugar de fallar más tarde.
/// </summary>
public static class ExtensionesOpciones
{
    /// <summary>Registra y valida todas las secciones de configuración.</summary>
    /// <param name="servicios">Colección de servicios.</param>
    /// <param name="configuracion">Configuración de la aplicación.</param>
    /// <returns>La misma colección, para encadenar llamadas.</returns>
    public static IServiceCollection AgregarOpciones(
        this IServiceCollection servicios,
        IConfiguration configuracion)
    {
        ArgumentNullException.ThrowIfNull(servicios);
        ArgumentNullException.ThrowIfNull(configuracion);

        servicios.AddOptions<JwtOptions>()
            .Bind(configuracion.GetSection(JwtOptions.Seccion))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        servicios.AddOptions<CifradoOptions>()
            .Bind(configuracion.GetSection(CifradoOptions.Seccion))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        servicios.AddOptions<AdjuntosOptions>()
            .Bind(configuracion.GetSection(AdjuntosOptions.Seccion))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        servicios.AddOptions<AlmacenObjetosOptions>()
            .Bind(configuracion.GetSection(AlmacenObjetosOptions.Seccion))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        servicios.AddOptions<CacheOptions>()
            .Bind(configuracion.GetSection(CacheOptions.Seccion))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        servicios.AddOptions<ValkeyOptions>()
            .Bind(configuracion.GetSection(ValkeyOptions.Seccion))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        servicios.AddOptions<SignalROptions>()
            .Bind(configuracion.GetSection(SignalROptions.Seccion))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        servicios.AddOptions<AdministradorOptions>()
            .Bind(configuracion.GetSection(AdministradorOptions.Seccion))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        return servicios;
    }
}
