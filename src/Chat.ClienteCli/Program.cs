using System.Text;
using Chat.ClienteCli.Comandos;
using Chat.ClienteCli.Infraestructura;
using Chat.ClienteCli.Servicios;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Spectre.Console;
using Spectre.Console.Cli;

// ---------------------------------------------------------------------------
// Codificación de la consola
// ---------------------------------------------------------------------------
// Windows abre la consola con una página de códigos heredada en la que un emoji
// sale como interrogaciones. Se fuerza UTF-8 en ambos sentidos: en la salida para
// pintarlos y en la entrada para poder pegarlos al escribir.
ConfigurarConsolaUtf8();

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
serviciosCompartidos.AddSingleton<RenderizadorImagenes>();
serviciosCompartidos.AddSingleton<VistaConversacion>();
serviciosCompartidos.AddSingleton<PantallaListaChats>();
serviciosCompartidos.AddSingleton<AplicacionChat>();

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
serviciosComandos.AddSingleton(proveedorCompartido.GetRequiredService<RenderizadorImagenes>());
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
        .WithExample("enviar", "General", "\"Hola a todos :fuego:\"");

    configurador.AddCommand<ComandoArchivo>("archivo")
        .WithDescription("Comparte un archivo en una sala; las imágenes se dibujan en la consola.")
        .WithExample("archivo", "General", "C:\\fotos\\grafico.png")
        .WithExample("archivo", "General", "informe.pdf", "-m", "\"Resultados de hoy\"");

    configurador.AddCommand<ComandoEmojis>("emojis")
        .WithDescription("Lista los atajos :nombre: que se pueden escribir en los mensajes.")
        .WithExample("emojis")
        .WithExample("emojis", "fuego");

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
// Sin argumentos se abre la aplicación de mensajería: lista de conversaciones,
// navegación con el teclado y refresco en vivo. Con argumentos se ejecuta una sola
// orden y se termina, que es lo que necesitan los guiones y la automatización.
// La consola de órdenes sigue disponible con «consola» para quien la prefiera.
if (args.Length == 0 && !Console.IsInputRedirected)
{
    return await EjecutarAplicacionAsync().ConfigureAwait(false);
}

if (args.Length == 1 && args[0].Equals("chat", StringComparison.OrdinalIgnoreCase))
{
    return await EjecutarAplicacionAsync().ConfigureAwait(false);
}

if (args.Length == 1 && args[0].Equals("consola", StringComparison.OrdinalIgnoreCase))
{
    return await new ConsolaInteractiva(aplicacion, api).EjecutarAsync().ConfigureAwait(false);
}

return await aplicacion.RunAsync(args).ConfigureAwait(false);

/// <summary>
/// Arranca la aplicación de mensajería y la deja atada a Ctrl+C, para que cerrar el
/// cliente cierre también la conexión con el hub de forma ordenada.
/// </summary>
async Task<int> EjecutarAplicacionAsync()
{
    using var cancelacion = new CancellationTokenSource();

    Console.CancelKeyPress += (_, evento) =>
    {
        evento.Cancel = true;
        cancelacion.Cancel();
    };

    try
    {
        return await proveedorCompartido
            .GetRequiredService<AplicacionChat>()
            .EjecutarAsync(cancelacion.Token)
            .ConfigureAwait(false);
    }
    catch (OperationCanceledException)
    {
        return 0;
    }
    catch (Exception excepcion)
    {
        Presentacion.Error(excepcion.Message);
        return 1;
    }
}

/// <summary>
/// Pone la consola en UTF-8 para que los emojis y los acentos se escriban y se lean
/// correctamente. Con la salida redirigida no hay consola que configurar, y en las
/// plataformas donde la operación no está soportada se ignora sin más: la aplicación
/// funciona igual, solo que el terminal decidirá cómo representar cada carácter.
/// </summary>
static void ConfigurarConsolaUtf8()
{
    try
    {
        if (!Console.IsOutputRedirected)
        {
            Console.OutputEncoding = Encoding.UTF8;
        }

        if (!Console.IsInputRedirected)
        {
            Console.InputEncoding = Encoding.UTF8;
        }
    }
    catch (Exception excepcion) when (excepcion is IOException or PlatformNotSupportedException)
    {
        // Sin terminal detrás (servicio, contenedor sin TTY) no hay nada que ajustar.
    }
}
