using System.ComponentModel;
using Chat.ClienteCli.Servicios;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Chat.ClienteCli.Comandos;

/// <summary>Muestra el historial reciente de una sala en forma de tabla.</summary>
public sealed class ComandoHistorial : ComandoBase<ComandoHistorial.Opciones>
{
    private readonly ClienteApi _api;

    /// <summary>Crea el comando.</summary>
    /// <param name="api">Cliente de la API.</param>
    public ComandoHistorial(ClienteApi api) => _api = api;

    /// <summary>Opciones del comando <c>historial</c>.</summary>
    public sealed class Opciones : CommandSettings
    {
        /// <summary>Sala consultada.</summary>
        [CommandArgument(0, "<sala>")]
        [Description("Nombre de la sala cuyo historial se consulta.")]
        public string Sala { get; init; } = string.Empty;

        /// <summary>Número de mensajes a mostrar.</summary>
        [CommandOption("-n|--cantidad")]
        [Description("Número de mensajes a mostrar (por defecto 50, máximo 200).")]
        [DefaultValue(50)]
        public int Cantidad { get; init; } = 50;
    }

    /// <inheritdoc />
    protected override async Task<int> EjecutarAsync(Opciones opciones, CancellationToken cancelacion)
    {
        await _api.RequerirSesionAsync(cancelacion).ConfigureAwait(false);
        var sala = await _api.ResolverSalaAsync(opciones.Sala, cancelacion).ConfigureAwait(false);

        var mensajes = await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(Style.Parse("deepskyblue1"))
            .StartAsync(
                "Descargando y descifrando el historial...",
                async _ => await _api.ObtenerMensajesAsync(sala.Id, opciones.Cantidad, cancelacion).ConfigureAwait(false))
            .ConfigureAwait(false);

        Presentacion.TablaMensajes(mensajes, sala.Nombre);
        return CodigoExito;
    }
}
