using Chat.AdminCli.Servicios;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Chat.AdminCli.Infraestructura;

/// <summary>
/// Consola administrativa interactiva: mantiene el proceso abierto leyendo órdenes
/// una tras otra y conservando la sesión de administrador entre ellas.
/// </summary>
public sealed class ConsolaAdminInteractiva
{
    /// <summary>Palabras que cierran la consola.</summary>
    private static readonly string[] OrdenesFin = ["terminar", "salir", "exit", "quit", "adios"];

    private readonly ICommandApp _aplicacion;
    private readonly ClienteAdminApi _api;

    /// <summary>Crea la consola interactiva.</summary>
    /// <param name="aplicacion">Aplicación de comandos ya configurada.</param>
    /// <param name="api">Cliente de la API administrativa.</param>
    public ConsolaAdminInteractiva(ICommandApp aplicacion, ClienteAdminApi api)
    {
        _aplicacion = aplicacion;
        _api = api;
    }

    /// <summary>Ejecuta el bucle de órdenes hasta que el usuario lo cierra.</summary>
    /// <returns>Código de salida del proceso.</returns>
    public async Task<int> EjecutarAsync()
    {
        MostrarBienvenida();

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
                PresentacionAdmin.LimpiarPantalla();
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
                PresentacionAdmin.Error(excepcion.Message);
            }

            AnsiConsole.WriteLine();
        }
    }

    /// <summary>Dibuja la cabecera y las indicaciones de uso.</summary>
    private void MostrarBienvenida()
    {
        AnsiConsole.Write(new Rule("[bold orange1]dotChat · consola de administración[/]").LeftJustified());
        AnsiConsole.MarkupLine($"[grey]Servidor: {PresentacionAdmin.Escapar(_api.UrlServidor)}[/]");
        AnsiConsole.MarkupLine(
            "[grey]La autenticación se pide la primera vez que ejecute una orden y se conserva mientras la consola siga abierta.[/]");
        AnsiConsole.MarkupLine(
            "[grey]Escriba una orden ([bold]ayuda[/] para verlas todas), " +
            "[bold]limpiar[/] para borrar la pantalla o [bold]terminar[/] para salir.[/]");
        AnsiConsole.WriteLine();
    }

    /// <summary>Muestra el indicador y lee una línea de la entrada estándar.</summary>
    /// <returns>La línea escrita, o <c>null</c> si se cerró la entrada.</returns>
    private string? LeerOrden()
    {
        var nombre = _api.NombreUsuario;

        AnsiConsole.Markup(nombre is null
            ? "[grey]admin[/][bold orange1]>[/] "
            : $"[grey]admin[/][bold orange1]:{PresentacionAdmin.Escapar(nombre)}>[/] ");

        return Console.ReadLine();
    }
}
