using Chat.AdminCli.Servicios;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Chat.AdminCli.Comandos;

/// <summary>
/// Base de todos los comandos administrativos. Centraliza tres cosas: la
/// autenticación previa, el tratamiento de errores y el indicador de espera.
/// </summary>
/// <remarks>
/// El orden importa. Spectre.Console solo admite una función interactiva a la vez,
/// así que la sesión se prepara —y, si hace falta, se piden las credenciales— antes
/// de que ninguna orden dibuje un indicador de progreso. Mezclar ambas cosas es lo
/// que producía el error «Trying to run one or more interactive functions concurrently».
/// </remarks>
/// <typeparam name="TOpciones">Tipo de las opciones del comando.</typeparam>
public abstract class ComandoAdminBase<TOpciones> : AsyncCommand<TOpciones>
    where TOpciones : CommandSettings
{
    /// <summary>Código de salida devuelto cuando la operación se completa.</summary>
    protected const int CodigoExito = 0;

    /// <summary>Código de salida devuelto cuando la operación falla.</summary>
    protected const int CodigoError = 1;

    /// <summary>Crea el comando.</summary>
    /// <param name="api">Cliente de la API administrativa, compartido por toda la consola.</param>
    protected ComandoAdminBase(ClienteAdminApi api) => Api = api;

    /// <summary>Cliente de la API administrativa.</summary>
    protected ClienteAdminApi Api { get; }

    /// <inheritdoc />
    protected sealed override async Task<int> ExecuteAsync(
        CommandContext contexto,
        TOpciones opciones,
        CancellationToken cancelacion)
    {
        try
        {
            // Único punto del programa donde se pueden pedir credenciales: aquí no
            // hay todavía ninguna pantalla activa de Spectre.Console.
            await Api.PrepararSesionAsync(cancelacion).ConfigureAwait(false);

            return await EjecutarAsync(opciones, cancelacion).ConfigureAwait(false);
        }
        catch (ExcepcionAdminApi excepcion)
        {
            PresentacionAdmin.Error(excepcion.Message);
            return CodigoError;
        }
        catch (HttpRequestException excepcion)
        {
            PresentacionAdmin.Error(
                $"No se pudo contactar con el servidor: {excepcion.Message} " +
                "Compruebe que está arrancado y que la dirección configurada es correcta.");
            return CodigoError;
        }
        catch (OperationCanceledException)
        {
            PresentacionAdmin.Aviso("Operación cancelada.");
            return CodigoError;
        }
    }

    /// <summary>Lógica concreta del comando.</summary>
    /// <param name="opciones">Opciones recibidas por línea de comandos.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    /// <returns>Código de salida del proceso.</returns>
    protected abstract Task<int> EjecutarAsync(TOpciones opciones, CancellationToken cancelacion);

    /// <summary>
    /// Ejecuta una operación de red mostrando un indicador de espera. Es seguro
    /// porque para cuando se llama la sesión ya está preparada y ninguna renovación
    /// posterior necesita teclado.
    /// </summary>
    /// <typeparam name="T">Tipo del resultado.</typeparam>
    /// <param name="mensaje">Texto que acompaña al indicador.</param>
    /// <param name="operacion">Operación a ejecutar.</param>
    protected static Task<T> ConEsperaAsync<T>(string mensaje, Func<Task<T>> operacion)
    {
        ArgumentNullException.ThrowIfNull(operacion);

        return AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(Style.Parse("orange1"))
            .StartAsync(mensaje, async _ => await operacion().ConfigureAwait(false));
    }
}

/// <summary>Opciones comunes a los comandos que no reciben parámetros.</summary>
public sealed class OpcionesVacias : CommandSettings;
