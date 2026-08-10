using System.ComponentModel;
using Chat.ClienteCli.Servicios;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Chat.ClienteCli.Comandos;

/// <summary>Crea una cuenta nueva en el servidor y deja la sesión iniciada.</summary>
public sealed class ComandoRegistro : ComandoBase<ComandoRegistro.Opciones>
{
    private readonly ClienteApi _api;

    /// <summary>Crea el comando.</summary>
    /// <param name="api">Cliente de la API.</param>
    public ComandoRegistro(ClienteApi api) => _api = api;

    /// <summary>Opciones del comando <c>registro</c>.</summary>
    public sealed class Opciones : CommandSettings
    {
        /// <summary>Nombre de usuario deseado.</summary>
        [CommandArgument(0, "[usuario]")]
        [Description("Nombre de usuario deseado (3-32 caracteres: letras, dígitos, . _ -).")]
        public string? Usuario { get; init; }

        /// <summary>Correo electrónico.</summary>
        [CommandOption("-e|--email")]
        [Description("Correo electrónico de la cuenta.")]
        public string? Email { get; init; }

        /// <summary>Contraseña.</summary>
        [CommandOption("-c|--clave")]
        [Description("Contraseña. Si se omite, se pide de forma oculta con confirmación.")]
        public string? Clave { get; init; }
    }

    /// <inheritdoc />
    protected override async Task<int> EjecutarAsync(Opciones opciones, CancellationToken cancelacion)
    {
        Presentacion.Cabecera($"Registro de cuenta en {_api.UrlServidor}");

        var usuario = opciones.Usuario
            ?? AnsiConsole.Prompt(new TextPrompt<string>("[bold]Usuario:[/]").PromptStyle("cyan"));

        var email = opciones.Email
            ?? AnsiConsole.Prompt(new TextPrompt<string>("[bold]Correo:[/]").PromptStyle("cyan"));

        var clave = opciones.Clave ?? PedirClaveConConfirmacion();

        var sesion = await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(Style.Parse("deepskyblue1"))
            .StartAsync(
                "Creando la cuenta...",
                async _ => await _api.RegistrarAsync(usuario, email, clave, cancelacion).ConfigureAwait(false))
            .ConfigureAwait(false);

        Presentacion.Exito("Cuenta creada correctamente.");
        Presentacion.PanelSesion(sesion);

        return CodigoExito;
    }

    /// <summary>Pide la contraseña dos veces y comprueba que coincidan.</summary>
    private static string PedirClaveConConfirmacion()
    {
        AnsiConsole.MarkupLine(
            "[grey]La contraseña debe tener al menos 10 caracteres, con mayúsculas, minúsculas, dígitos y símbolos.[/]");

        while (true)
        {
            var clave = AnsiConsole.Prompt(
                new TextPrompt<string>("[bold]Contraseña:[/]").PromptStyle("cyan").Secret());

            var confirmacion = AnsiConsole.Prompt(
                new TextPrompt<string>("[bold]Repita la contraseña:[/]").PromptStyle("cyan").Secret());

            if (string.Equals(clave, confirmacion, StringComparison.Ordinal))
            {
                return clave;
            }

            Presentacion.Aviso("Las contraseñas no coinciden. Inténtelo de nuevo.");
        }
    }
}
