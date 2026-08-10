using Chat.ClienteCli.Servicios;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Chat.ClienteCli.Infraestructura;

/// <summary>
/// Consola interactiva del cliente: mantiene el proceso abierto leyendo órdenes
/// una tras otra, en lugar de terminar tras ejecutar una sola.
/// </summary>
/// <remarks>
/// Reutiliza la misma definición de comandos que el modo de una sola orden, de modo
/// que no hay dos rutas de ejecución que mantener. La sesión y la conexión de SignalR
/// se conservan entre órdenes, por lo que no hay que reautenticarse en cada una.
/// </remarks>
public sealed class ConsolaInteractiva
{
    /// <summary>Palabras que cierran la consola.</summary>
    private static readonly string[] OrdenesFin = ["terminar", "exit", "quit", "adios"];

    private readonly ICommandApp _aplicacion;
    private readonly ClienteApi _api;

    /// <summary>Crea la consola interactiva.</summary>
    /// <param name="aplicacion">Aplicación de comandos ya configurada.</param>
    /// <param name="api">Cliente de la API, usado para mostrar el estado de la sesión.</param>
    public ConsolaInteractiva(ICommandApp aplicacion, ClienteApi api)
    {
        _aplicacion = aplicacion;
        _api = api;
    }

    /// <summary>Ejecuta el bucle de órdenes hasta que el usuario lo cierra.</summary>
    /// <returns>Código de salida del proceso.</returns>
    public async Task<int> EjecutarAsync()
    {
        await MostrarBienvenidaAsync().ConfigureAwait(false);

        while (true)
        {
            var linea = LeerOrden();

            if (linea is null)
            {
                // Fin de la entrada estándar (Ctrl+D o tubería agotada).
                return 0;
            }

            var argumentos = DivisorArgumentos.Dividir(linea);

            if (argumentos.Length == 0)
            {
                continue;
            }

            if (OrdenesFin.Contains(argumentos[0], StringComparer.OrdinalIgnoreCase))
            {
                AnsiConsole.MarkupLine("[grey]Hasta luego.[/]");
                return 0;
            }

            if (argumentos[0].Equals("limpiar", StringComparison.OrdinalIgnoreCase))
            {
                Presentacion.LimpiarPantalla();
                continue;
            }

            if (argumentos[0].Equals("ayuda", StringComparison.OrdinalIgnoreCase))
            {
                argumentos = ["--help"];
            }

            try
            {
                await _aplicacion.RunAsync(argumentos).ConfigureAwait(false);
            }
            catch (Exception excepcion)
            {
                // Un fallo en una orden no debe cerrar la consola.
                Presentacion.Error(excepcion.Message);
            }

            AnsiConsole.WriteLine();
        }
    }

    /// <summary>Dibuja el rótulo, el estado de la sesión y un resumen de lo pendiente.</summary>
    private async Task MostrarBienvenidaAsync()
    {
        Presentacion.Banner(_api.UrlServidor);

        var sesion = await _api.CargarSesionAsync().ConfigureAwait(false);

        if (sesion is null)
        {
            AnsiConsole.MarkupLine("[yellow]No hay sesión iniciada. Empiece con [bold]login[/] o [bold]registro[/].[/]");
        }
        else
        {
            AnsiConsole.MarkupLine($"[green]Sesión activa como [bold]{Presentacion.Escapar(sesion.NombreUsuario)}[/].[/]");
            await MostrarPendientesAsync().ConfigureAwait(false);
        }

        AnsiConsole.MarkupLine(
            "[grey]Órdenes: [bold]salas[/], [bold]unirse[/] <sala>, [bold]privado[/] <usuario>, " +
            "[bold]usuarios[/], [bold]historial[/] <sala>.[/]");
        AnsiConsole.MarkupLine(
            "[grey]Escriba [bold]ayuda[/] para verlas todas, [bold]limpiar[/] para borrar la pantalla " +
            "o [bold]terminar[/] para salir.[/]");
        AnsiConsole.WriteLine();
    }

    /// <summary>
    /// Avisa de los mensajes pendientes al abrir la consola. Es informativo: si el
    /// servidor no responde, la consola se abre igual y no se muestra nada.
    /// </summary>
    private async Task MostrarPendientesAsync()
    {
        try
        {
            var salas = await _api.ObtenerSalasPropiasAsync().ConfigureAwait(false);
            var pendientes = salas.Sum(s => s.MensajesSinLeer);

            if (pendientes == 0)
            {
                return;
            }

            var conversaciones = salas.Count(s => s.MensajesSinLeer > 0);

            AnsiConsole.MarkupLine(
                $"[bold red]● {pendientes}[/] [grey]mensaje(s) sin leer en {conversaciones} conversación(es). " +
                "Use [bold]salas --mias[/] para verlas.[/]");
        }
        catch (Exception excepcion) when (excepcion is ExcepcionApi or HttpRequestException)
        {
            // El servidor no está disponible: la consola sigue siendo utilizable.
        }
    }

    /// <summary>Muestra el indicador y lee una línea de la entrada estándar.</summary>
    /// <returns>La línea escrita, o <c>null</c> si se cerró la entrada.</returns>
    private string? LeerOrden()
    {
        var nombre = _api.Sesion?.NombreUsuario;

        AnsiConsole.Markup(nombre is null
            ? $"[grey]chat[/][bold {Presentacion.ColorPrincipal}]>[/] "
            : $"[grey]chat[/][bold {Presentacion.ColorPrincipal}]:{Presentacion.Escapar(nombre)}>[/] ");

        return Console.ReadLine();
    }
}
