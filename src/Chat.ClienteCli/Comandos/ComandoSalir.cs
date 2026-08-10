using System.ComponentModel;
using Chat.ClienteCli.Servicios;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Chat.ClienteCli.Comandos;

/// <summary>
/// Abandona una sala. Con <c>--sesion</c> cierra además la sesión local,
/// borrando los tokens guardados en el perfil del usuario.
/// </summary>
public sealed class ComandoSalir : ComandoBase<ComandoSalir.Opciones>
{
    private readonly ClienteApi _api;

    /// <summary>Crea el comando.</summary>
    /// <param name="api">Cliente de la API.</param>
    public ComandoSalir(ClienteApi api) => _api = api;

    /// <summary>Opciones del comando <c>salir</c>.</summary>
    public sealed class Opciones : CommandSettings
    {
        /// <summary>Nombre de la sala que se abandona.</summary>
        [CommandArgument(0, "[sala]")]
        [Description("Nombre de la sala que se abandona.")]
        public string? Sala { get; init; }

        /// <summary>Cierra la sesión local.</summary>
        [CommandOption("--sesion")]
        [Description("Cierra la sesión local y borra los tokens guardados.")]
        public bool CerrarSesion { get; init; }
    }

    /// <inheritdoc />
    protected override async Task<int> EjecutarAsync(Opciones opciones, CancellationToken cancelacion)
    {
        if (opciones.CerrarSesion)
        {
            _api.CerrarSesion();
            Presentacion.Exito("Sesión cerrada; se han borrado los tokens locales.");

            if (string.IsNullOrWhiteSpace(opciones.Sala))
            {
                return CodigoExito;
            }
        }

        if (string.IsNullOrWhiteSpace(opciones.Sala))
        {
            Presentacion.Aviso("Indique la sala que desea abandonar, o use '--sesion' para cerrar la sesión.");
            return CodigoError;
        }

        await _api.RequerirSesionAsync(cancelacion).ConfigureAwait(false);
        var sala = await _api.ResolverSalaAsync(opciones.Sala, cancelacion).ConfigureAwait(false);

        var resultado = await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync(
                $"Saliendo de '{sala.Nombre}'...",
                async _ => await _api.SalirSalaAsync(sala.Id, cancelacion).ConfigureAwait(false))
            .ConfigureAwait(false);

        Presentacion.Exito(resultado.Mensaje);
        return CodigoExito;
    }
}
