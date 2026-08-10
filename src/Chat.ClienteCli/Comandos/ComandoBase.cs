using Chat.ClienteCli.Servicios;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Chat.ClienteCli.Comandos;

/// <summary>
/// Base de todos los comandos del cliente. Concentra el tratamiento de errores
/// para que cada comando se limite a su lógica y nunca muestre una traza cruda.
/// </summary>
/// <typeparam name="TOpciones">Tipo de las opciones del comando.</typeparam>
public abstract class ComandoBase<TOpciones> : AsyncCommand<TOpciones>
    where TOpciones : CommandSettings
{
    /// <summary>Código de salida devuelto cuando la operación se completa.</summary>
    protected const int CodigoExito = 0;

    /// <summary>Código de salida devuelto cuando la operación falla.</summary>
    protected const int CodigoError = 1;

    /// <inheritdoc />
    protected sealed override async Task<int> ExecuteAsync(
        CommandContext contexto,
        TOpciones opciones,
        CancellationToken cancelacion)
    {
        try
        {
            // Spectre.Console.Cli enlaza este token con Ctrl+C, de modo que la
            // cancelación llega a todas las llamadas de red en curso.
            return await EjecutarAsync(opciones, cancelacion).ConfigureAwait(false);
        }
        catch (ExcepcionApi excepcion)
        {
            Presentacion.Error(excepcion.Message);

            foreach (var (campo, mensajes) in excepcion.Errores)
            {
                foreach (var mensaje in mensajes)
                {
                    AnsiConsole.MarkupLine($"  [red]·[/] [bold]{Presentacion.Escapar(campo)}[/]: {Presentacion.Escapar(mensaje)}");
                }
            }

            return CodigoError;
        }
        catch (HttpRequestException excepcion)
        {
            Presentacion.Error(
                $"No se pudo contactar con el servidor: {excepcion.Message} " +
                "Compruebe que está arrancado y que la dirección configurada es correcta.");
            return CodigoError;
        }
        catch (OperationCanceledException)
        {
            Presentacion.Aviso("Operación cancelada.");
            return CodigoError;
        }
    }

    /// <summary>Lógica concreta del comando.</summary>
    /// <param name="opciones">Opciones recibidas por línea de comandos.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    /// <returns>Código de salida del proceso.</returns>
    protected abstract Task<int> EjecutarAsync(TOpciones opciones, CancellationToken cancelacion);
}
