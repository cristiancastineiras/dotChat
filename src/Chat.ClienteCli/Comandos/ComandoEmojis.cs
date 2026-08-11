using System.ComponentModel;
using Chat.Aplicacion.Mensajeria;
using Chat.ClienteCli.Servicios;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Chat.ClienteCli.Comandos;

/// <summary>
/// Muestra el catálogo de atajos de emoji, o busca dentro de él.
/// </summary>
/// <remarks>
/// El catálogo es el mismo tipo que usa el servidor para expandirlos, así que lo que
/// se lista aquí es exactamente lo que se va a sustituir al enviar el mensaje.
/// </remarks>
public sealed class ComandoEmojis : ComandoBase<ComandoEmojis.Opciones>
{
    /// <summary>Opciones del comando <c>emojis</c>.</summary>
    public sealed class Opciones : CommandSettings
    {
        /// <summary>Texto por el que filtrar.</summary>
        [CommandArgument(0, "[busqueda]")]
        [Description("Filtra el catálogo por nombre, alias o categoría.")]
        public string? Busqueda { get; init; }
    }

    /// <inheritdoc />
    protected override Task<int> EjecutarAsync(Opciones opciones, CancellationToken cancelacion)
    {
        if (string.IsNullOrWhiteSpace(opciones.Busqueda))
        {
            Presentacion.TablaEmojis();
            return Task.FromResult(CodigoExito);
        }

        var busqueda = opciones.Busqueda.Trim();

        var coincidencias = CatalogoEmojis.Entradas
            .Where(entrada =>
                entrada.Nombre.Contains(busqueda, StringComparison.OrdinalIgnoreCase)
                || entrada.Categoria.Contains(busqueda, StringComparison.OrdinalIgnoreCase)
                || entrada.Alias.Any(alias => alias.Contains(busqueda, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (coincidencias.Count == 0)
        {
            Presentacion.Aviso($"Ningún emoji coincide con '{busqueda}'. Ejecute 'emojis' para verlos todos.");
            return Task.FromResult(CodigoError);
        }

        var tabla = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Grey37)
            .Title($"[bold]Coincidencias con «{Presentacion.Escapar(busqueda)}» ({coincidencias.Count})[/]");

        tabla.AddColumn(new TableColumn("[bold]Emoji[/]").Centered());
        tabla.AddColumn("[bold]Atajo[/]");
        tabla.AddColumn("[bold]Alias[/]");
        tabla.AddColumn("[bold]Categoría[/]");

        foreach (var entrada in coincidencias)
        {
            tabla.AddRow(
                entrada.Simbolo,
                $"[{Presentacion.ColorPrincipal}]:{Presentacion.Escapar(entrada.Nombre)}:[/]",
                entrada.Alias.Length == 0
                    ? "[grey]-[/]"
                    : $"[grey]{Presentacion.Escapar(string.Join(", ", entrada.Alias))}[/]",
                $"[grey]{Presentacion.Escapar(entrada.Categoria)}[/]");
        }

        AnsiConsole.Write(tabla);

        return Task.FromResult(CodigoExito);
    }
}
