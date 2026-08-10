using Chat.ClienteCli.Comandos;
using Chat.ClienteCli.Infraestructura;
using Chat.ClienteCli.Servicios;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Spectre.Console;
using Spectre.Console.Cli;

// ---------------------------------------------------------------------------
// Configuración: appsettings.json del cliente + variables de entorno DOTCHAT_
// ---------------------------------------------------------------------------
var configuracion = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables("DOTCHAT_")
    .Build();

var opciones = configuracion.GetSection(OpcionesCliente.Seccion).Get<OpcionesCliente>() ?? new OpcionesCliente();

// ---------------------------------------------------------------------------
// Servicios de larga vida.
// Se construyen una sola vez y se comparten entre todas las órdenes: así la
// consola interactiva conserva la sesión y la conexión de SignalR entre comandos.
// ---------------------------------------------------------------------------
var serviciosCompartidos = new ServiceCollection();

serviciosCompartidos.AddOptions<OpcionesCliente>()
    .Bind(configuracion.GetSection(OpcionesCliente.Seccion))
    .ValidateDataAnnotations();

serviciosCompartidos.AddSingleton<AlmacenSesion>();
serviciosCompartidos.AddSingleton<ClienteTiempoReal>();
serviciosCompartidos.AddSingleton<VistaConversacion>();

serviciosCompartidos.AddHttpClient<ClienteApi>(cliente =>
    {
        cliente.BaseAddress = new Uri(opciones.UrlServidor);
        cliente.Timeout = TimeSpan.FromSeconds(opciones.SegundosTiempoEspera);
        cliente.DefaultRequestHeaders.Add("User-Agent", "dotChat-ClienteCli");
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

var api = proveedorCompartido.GetRequiredService<ClienteApi>();

// Los comandos se resuelven en un contenedor nuevo por ejecución (así lo hace
// Spectre.Console.Cli), pero reciben estas instancias ya creadas y compartidas.
var serviciosComandos = new ServiceCollection();
serviciosComandos.AddSingleton(api);
serviciosComandos.AddSingleton(proveedorCompartido.GetRequiredService<ClienteTiempoReal>());
serviciosComandos.AddSingleton(proveedorCompartido.GetRequiredService<VistaConversacion>());
serviciosComandos.AddSingleton(proveedorCompartido.GetRequiredService<IOptions<OpcionesCliente>>());

var aplicacion = new CommandApp(new RegistradorTipos(serviciosComandos));

aplicacion.Configure(configurador =>
{
    configurador.SetApplicationName("chat");
    configurador.UseStrictParsing();
    configurador.ValidateExamples();

    configurador.AddCommand<ComandoLogin>("login")
        .WithDescription("Inicia sesión en el servidor y guarda la sesión localmente.")
        .WithExample("login", "ana")
        .WithExample("login");

    configurador.AddCommand<ComandoRegistro>("registro")
        .WithDescription("Crea una cuenta nueva y deja la sesión iniciada.")
        .WithExample("registro", "ana", "--email", "ana@ejemplo.local");

    configurador.AddCommand<ComandoUnirse>("unirse")
        .WithDescription("Se une a una sala y abre la conversación en tiempo real.")
        .WithExample("unirse", "General")
        .WithExample("unirse", "General", "--sin-chat");

    configurador.AddCommand<ComandoPrivado>("privado")
        .WithDescription("Abre una conversación privada con otra persona.")
        .WithExample("privado", "ana")
        .WithExample("privado", "ana", "-m", "\"¿Tienes un momento?\"");

    configurador.AddCommand<ComandoSalir>("salir")
        .WithDescription("Abandona una sala o cierra la sesión local.")
        .WithExample("salir", "General")
        .WithExample("salir", "--sesion");

    configurador.AddCommand<ComandoEnviar>("enviar")
        .WithDescription("Envía un mensaje puntual a una sala.")
        .WithExample("enviar", "General", "\"Hola a todos\"");

    configurador.AddCommand<ComandoHistorial>("historial")
        .WithDescription("Muestra el historial reciente de una sala.")
        .WithExample("historial", "General", "-n", "20");

    configurador.AddCommand<ComandoUsuarios>("usuarios")
        .WithDescription("Lista los usuarios de la plataforma y quién está en línea.")
        .WithExample("usuarios")
        .WithExample("usuarios", "--conectados");

    configurador.AddCommand<ComandoSalas>("salas")
        .WithDescription("Muestra tus conversaciones y el catálogo de salas; permite crear una nueva.")
        .WithExample("salas")
        .WithExample("salas", "--mias")
        .WithExample("salas", "--crear", "Proyectos", "-d", "\"Coordinación del equipo\"")
        .WithExample("salas", "--crear", "Direccion", "--privada");

    configurador.SetExceptionHandler((excepcion, _) =>
    {
        // Red de seguridad: cualquier fallo no previsto se muestra formateado,
        // nunca como una traza cruda de .NET.
        Presentacion.Error(excepcion.Message);
        return 1;
    });
});

// ---------------------------------------------------------------------------
// Modo de ejecución
// ---------------------------------------------------------------------------
// Sin argumentos se abre la consola interactiva y el proceso queda esperando
// órdenes. Con argumentos se ejecuta una sola orden y se termina, que es lo que
// necesitan los guiones y la automatización.
if (args.Length == 0 && !Console.IsInputRedirected)
{
    return await new ConsolaInteractiva(aplicacion, api).EjecutarAsync().ConfigureAwait(false);
}

if (args.Length == 1 && args[0].Equals("consola", StringComparison.OrdinalIgnoreCase))
{
    return await new ConsolaInteractiva(aplicacion, api).EjecutarAsync().ConfigureAwait(false);
}

return await aplicacion.RunAsync(args).ConfigureAwait(false);
