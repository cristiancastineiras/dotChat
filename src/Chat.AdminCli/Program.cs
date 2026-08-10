using Chat.AdminCli.Comandos.Cache;
using Chat.AdminCli.Comandos.Diagnostico;
using Chat.AdminCli.Comandos.Mensajes;
using Chat.AdminCli.Comandos.Salas;
using Chat.AdminCli.Comandos.Usuarios;
using Chat.AdminCli.Infraestructura;
using Chat.AdminCli.Servicios;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli;


// ---------------------------------------------------------------------------
// Configuración: appsettings.json + variables de entorno DOTCHAT_
// La contraseña del administrador se toma de DOTCHAT_Admin__Clave o se pide por consola.
// ---------------------------------------------------------------------------
var configuracion = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables("DOTCHAT_")
    .Build();

var opciones = configuracion.GetSection(OpcionesAdmin.Seccion).Get<OpcionesAdmin>() ?? new OpcionesAdmin();

// ---------------------------------------------------------------------------
// Servicios de larga vida.
// Se construyen una sola vez para que la consola interactiva conserve la sesión
// de administrador entre órdenes y no vuelva a pedir la contraseña.
// ---------------------------------------------------------------------------
var serviciosCompartidos = new ServiceCollection();

serviciosCompartidos.AddOptions<OpcionesAdmin>()
    .Bind(configuracion.GetSection(OpcionesAdmin.Seccion))
    .ValidateDataAnnotations();

serviciosCompartidos.AddHttpClient<ClienteAdminApi>(cliente =>
    {
        cliente.BaseAddress = new Uri(opciones.UrlServidor);
        cliente.Timeout = TimeSpan.FromSeconds(opciones.SegundosTiempoEspera);
        cliente.DefaultRequestHeaders.Add("User-Agent", "dotChat-AdminCli");
    })
    .ConfigurePrimaryHttpMessageHandler(() =>
    {
        var manejador = new HttpClientHandler();

        // Solo para desarrollo contra certificados autofirmados no instalados.
        if (opciones.AceptarCertificadosNoConfiables)
        {
            manejador.ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
        }

        return manejador;
    });

await using var proveedorCompartido = serviciosCompartidos.BuildServiceProvider();

var api = proveedorCompartido.GetRequiredService<ClienteAdminApi>();

// Los comandos se resuelven en un contenedor nuevo por ejecución (así lo hace
// Spectre.Console.Cli), pero reciben esta instancia ya creada y compartida.
var serviciosComandos = new ServiceCollection();
serviciosComandos.AddSingleton(api);

var aplicacion = new CommandApp(new RegistradorTipos(serviciosComandos));

aplicacion.Configure(configurador =>
{
    configurador.SetApplicationName("chatadmin");
    configurador.UseStrictParsing();
    configurador.ValidateExamples();

    configurador.AddBranch("usuarios", rama =>
    {
        rama.SetDescription("Gestión de cuentas de usuario.");

        rama.AddCommand<ComandoUsuariosListar>("listar")
            .WithDescription("Lista todas las cuentas registradas.")
            .WithExample("usuarios", "listar");

        rama.AddCommand<ComandoUsuariosEliminar>("eliminar")
            .WithDescription("Elimina definitivamente una cuenta y sus datos.")
            .WithExample("usuarios", "eliminar", "ana");
    });

    configurador.AddBranch("salas", rama =>
    {
        rama.SetDescription("Gestión de salas de conversación.");

        rama.AddCommand<ComandoSalasListar>("listar")
            .WithDescription("Lista todas las salas.")
            .WithExample("salas", "listar");

        rama.AddCommand<ComandoSalasCrear>("crear")
            .WithDescription("Crea una sala nueva, pública o privada.")
            .WithExample("salas", "crear", "Incidencias", "-d", "\"Guardia de soporte\"")
            .WithExample("salas", "crear", "Direccion", "--privada");

        rama.AddCommand<ComandoSalasMiembros>("miembros")
            .WithDescription("Muestra quién compone una sala y quién está conectado.")
            .WithExample("salas", "miembros", "Incidencias");

        rama.AddCommand<ComandoSalasEliminar>("eliminar")
            .WithDescription("Elimina una sala y todo su historial.")
            .WithExample("salas", "eliminar", "Incidencias");
    });

    configurador.AddBranch("mensajes", rama =>
    {
        rama.SetDescription("Auditoría de mensajes.");

        rama.AddCommand<ComandoMensajesListar>("listar")
            .WithDescription("Muestra el historial descifrado de una sala.")
            .WithExample("mensajes", "listar", "General", "-n", "100");
    });

    configurador.AddBranch("cache", rama =>
    {
        rama.SetDescription("Mantenimiento de la caché del servidor.");

        rama.AddCommand<ComandoCacheLimpiar>("limpiar")
            .WithDescription("Vacía por completo la caché.")
            .WithExample("cache", "limpiar");
    });

    configurador.AddCommand<ComandoEstadisticas>("estadisticas")
        .WithDescription("Muestra un resumen de actividad y quién está en línea.")
        .WithExample("estadisticas");

    configurador.AddCommand<ComandoMostrarConexiones>("mostrar-conexiones")
        .WithDescription("Lista las conexiones SignalR abiertas en este momento.")
        .WithExample("mostrar-conexiones")
        .WithExample("mostrar-conexiones", "--seguir");

    configurador.SetExceptionHandler((excepcion, _) =>
    {
        // Red de seguridad: ningún fallo inesperado debe mostrarse como traza cruda.
        PresentacionAdmin.Error(excepcion.Message);
        return 1;
    });
});

// ---------------------------------------------------------------------------
// Modo de ejecución
// ---------------------------------------------------------------------------
// Sin argumentos se abre la consola interactiva y el proceso queda esperando
// órdenes. Con argumentos se ejecuta una sola orden y se termina, que es lo que
// necesitan los guiones y la automatización.
if ((args.Length == 0 && !Console.IsInputRedirected)
    || (args.Length == 1 && args[0].Equals("consola", StringComparison.OrdinalIgnoreCase)))
{
    return await new ConsolaAdminInteractiva(aplicacion, api).EjecutarAsync().ConfigureAwait(false);
}

return await aplicacion.RunAsync(args).ConfigureAwait(false);
