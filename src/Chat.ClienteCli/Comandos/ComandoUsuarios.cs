using System.ComponentModel;
using Chat.ClienteCli.Servicios;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Chat.ClienteCli.Comandos;

/// <summary>Lista los usuarios registrados en la plataforma y quién está conectado.</summary>
public sealed class ComandoUsuarios : ComandoBase<ComandoUsuarios.Opciones>
{
    private readonly ClienteApi _api;

    /// <summary>Crea el comando.</summary>
    /// <param name="api">Cliente de la API.</param>
    public ComandoUsuarios(ClienteApi api) => _api = api;

    /// <summary>Opciones del comando <c>usuarios</c>.</summary>
    public sealed class Opciones : CommandSettings
    {
        /// <summary>Muestra solo los usuarios conectados.</summary>
        [CommandOption("-c|--conectados")]
        [Description("Muestra únicamente a quienes están en línea en este momento.")]
        public bool SoloConectados { get; init; }
    }

    /// <inheritdoc />
    protected override async Task<int> EjecutarAsync(Opciones opciones, CancellationToken cancelacion)
    {
        await _api.RequerirSesionAsync(cancelacion).ConfigureAwait(false);

        var usuarios = await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(Style.Parse(Presentacion.ColorPrincipal))
            .StartAsync(
                "Consultando usuarios...",
                async _ => await _api.ObtenerUsuariosAsync(cancelacion).ConfigureAwait(false))
            .ConfigureAwait(false);

        if (opciones.SoloConectados)
        {
            usuarios = [.. usuarios.Where(u => u.EnLinea)];

            if (usuarios.Count == 0)
            {
                Presentacion.Aviso("Ahora mismo no hay nadie conectado.");
                return CodigoExito;
            }
        }

        Presentacion.TablaUsuarios(usuarios);
        return CodigoExito;
    }
}
