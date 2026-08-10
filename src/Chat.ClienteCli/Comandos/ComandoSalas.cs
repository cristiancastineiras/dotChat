using System.ComponentModel;
using Chat.ClienteCli.Servicios;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Chat.ClienteCli.Comandos;

/// <summary>
/// Muestra la bandeja del usuario y el catálogo de salas y, opcionalmente, crea
/// una sala nueva.
/// </summary>
public sealed class ComandoSalas : ComandoBase<ComandoSalas.Opciones>
{
    private readonly ClienteApi _api;

    /// <summary>Crea el comando.</summary>
    /// <param name="api">Cliente de la API.</param>
    public ComandoSalas(ClienteApi api) => _api = api;

    /// <summary>Opciones del comando <c>salas</c>.</summary>
    public sealed class Opciones : CommandSettings
    {
        /// <summary>Nombre de una sala a crear.</summary>
        [CommandOption("--crear")]
        [Description("Crea una sala nueva con el nombre indicado.")]
        public string? Crear { get; init; }

        /// <summary>Descripción de la sala que se crea.</summary>
        [CommandOption("-d|--descripcion")]
        [Description("Descripción de la sala que se crea.")]
        public string? Descripcion { get; init; }

        /// <summary>Crea la sala como privada.</summary>
        [CommandOption("-p|--privada")]
        [Description("Crea la sala como privada: solo la verán quienes invites.")]
        public bool Privada { get; init; }

        /// <summary>Muestra solo las salas propias.</summary>
        [CommandOption("--mias")]
        [Description("Muestra únicamente tus conversaciones, con los mensajes pendientes.")]
        public bool SoloMias { get; init; }
    }

    /// <inheritdoc />
    protected override async Task<int> EjecutarAsync(Opciones opciones, CancellationToken cancelacion)
    {
        await _api.RequerirSesionAsync(cancelacion).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(opciones.Crear))
        {
            var creada = await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .SpinnerStyle(Style.Parse(Presentacion.ColorPrincipal))
                .StartAsync(
                    $"Creando la sala '{opciones.Crear}'...",
                    async _ => await _api
                        .CrearSalaAsync(opciones.Crear, opciones.Descripcion, opciones.Privada, cancelacion)
                        .ConfigureAwait(false))
                .ConfigureAwait(false);

            Presentacion.Exito(opciones.Privada
                ? $"Sala privada '{creada.Nombre}' creada. Invita con '/invitar <usuario>' dentro de la sala."
                : $"Sala '{creada.Nombre}' creada. Ya eres miembro de ella.");
        }

        var propias = await _api.ObtenerSalasPropiasAsync(cancelacion).ConfigureAwait(false);
        Presentacion.TablaBandeja(propias);

        if (opciones.SoloMias)
        {
            return CodigoExito;
        }

        AnsiConsole.WriteLine();

        var catalogo = await _api.ObtenerSalasAsync(cancelacion).ConfigureAwait(false);
        Presentacion.TablaSalas(catalogo, propias.Select(s => s.Id).ToHashSet());

        return CodigoExito;
    }
}
