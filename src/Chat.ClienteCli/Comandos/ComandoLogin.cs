using System.ComponentModel;
using Chat.ClienteCli.Servicios;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Chat.ClienteCli.Comandos;

/// <summary>Inicia sesión en el servidor y guarda los tokens en el perfil del usuario.</summary>
public sealed class ComandoLogin : ComandoBase<ComandoLogin.Opciones>
{
    private readonly ClienteApi _api;

    /// <summary>Crea el comando.</summary>
    /// <param name="api">Cliente de la API.</param>
    public ComandoLogin(ClienteApi api) => _api = api;

    /// <summary>Opciones del comando <c>login</c>.</summary>
    public sealed class Opciones : CommandSettings
    {
        /// <summary>Nombre de usuario.</summary>
        [CommandArgument(0, "[usuario]")]
        [Description("Nombre de usuario. Si se omite, se pregunta de forma interactiva.")]
        public string? Usuario { get; init; }

        /// <summary>Contraseña.</summary>
        [CommandOption("-c|--clave")]
        [Description("Contraseña. Si se omite, se pide de forma oculta (recomendado).")]
        public string? Clave { get; init; }
    }

    /// <inheritdoc />
    protected override async Task<int> EjecutarAsync(Opciones opciones, CancellationToken cancelacion)
    {
        Presentacion.Cabecera($"Inicio de sesión en {_api.UrlServidor}");

        var usuario = opciones.Usuario
            ?? AnsiConsole.Prompt(new TextPrompt<string>("[bold]Usuario:[/]").PromptStyle("cyan"));

        // La contraseña nunca se muestra por pantalla ni queda en el historial del terminal.
        var clave = opciones.Clave
            ?? AnsiConsole.Prompt(new TextPrompt<string>("[bold]Contraseña:[/]").PromptStyle("cyan").Secret());

        var sesion = await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(Style.Parse("deepskyblue1"))
            .StartAsync(
                "Autenticando...",
                async _ => await _api.IniciarSesionAsync(usuario, clave, cancelacion).ConfigureAwait(false))
            .ConfigureAwait(false);

        Presentacion.PanelSesion(sesion);
        return CodigoExito;
    }
}
