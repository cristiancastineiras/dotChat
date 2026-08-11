using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using Chat.Aplicacion.Observabilidad;

namespace Chat.Aplicacion.Cqrs;

/// <summary>
/// Implementación del despachador basada en el contenedor de dependencias.
/// Cachea por tipo de mensaje tanto el tipo del manejador como el método a invocar,
/// de modo que la reflexión solo se paga la primera vez.
/// </summary>
public sealed class Despachador : IDespachador
{
    private static readonly ConcurrentDictionary<(Type Mensaje, Type Resultado), DatosManejador> Cache = new();

    private readonly IServiceProvider _proveedor;

    /// <summary>Crea el despachador.</summary>
    /// <param name="proveedor">Contenedor desde el que se resuelven los manejadores.</param>
    public Despachador(IServiceProvider proveedor) => _proveedor = proveedor;

    /// <inheritdoc />
    public Task<TResultado> EjecutarAsync<TResultado>(IComando<TResultado> comando, CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(comando);
        return DespacharAsync<TResultado>(comando, typeof(IManejadorComando<,>), cancelacion);
    }

    /// <inheritdoc />
    public Task<TResultado> ConsultarAsync<TResultado>(IConsulta<TResultado> consulta, CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(consulta);
        return DespacharAsync<TResultado>(consulta, typeof(IManejadorConsulta<,>), cancelacion);
    }

    /// <summary>Resuelve el manejador correspondiente al mensaje, lo invoca y lo traza.</summary>
    /// <typeparam name="TResultado">Tipo del resultado esperado.</typeparam>
    /// <param name="mensaje">Comando o consulta a despachar.</param>
    /// <param name="tipoManejadorAbierto">Interfaz genérica abierta del manejador.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    private async Task<TResultado> DespacharAsync<TResultado>(
        object mensaje,
        Type tipoManejadorAbierto,
        CancellationToken cancelacion)
    {
        var datos = Cache.GetOrAdd(
            (mensaje.GetType(), typeof(TResultado)),
            clave => DatosManejador.Crear(tipoManejadorAbierto, clave.Mensaje, clave.Resultado));

        var manejador = _proveedor.GetService(datos.TipoManejador)
            ?? throw new InvalidOperationException(
                $"No hay ningún manejador registrado para '{mensaje.GetType().Name}'. " +
                $"Registre una implementación de '{datos.TipoManejador.Name}' en el contenedor.");

        // Sin receptores de telemetría escuchando, StartActivity devuelve null y
        // el coste de este bloque es una comprobación de referencia nula.
        using var actividad = TrazasAplicacion.Fuente.StartActivity(datos.NombreActividad);
        actividad?.SetTag("cqrs.mensaje", datos.NombreMensaje);
        actividad?.SetTag("cqrs.tipo", datos.EsComando ? "comando" : "consulta");

        try
        {
            return await ((Task<TResultado>)datos.Metodo.Invoke(manejador, [mensaje, cancelacion])!)
                .ConfigureAwait(false);
        }
        catch (Exception excepcion)
        {
            actividad?.SetStatus(ActivityStatusCode.Error, excepcion.Message);
            actividad?.SetTag("error.type", excepcion.GetType().FullName);
            throw;
        }
    }

    /// <summary>Metadatos de invocación cacheados para un par (mensaje, resultado).</summary>
    /// <param name="TipoManejador">Tipo cerrado de la interfaz del manejador.</param>
    /// <param name="Metodo">Método <c>ManejarAsync</c> a invocar.</param>
    /// <param name="NombreActividad">Nombre de la traza asociada al mensaje.</param>
    /// <param name="NombreMensaje">Nombre del comando o consulta, para etiquetar la traza.</param>
    /// <param name="EsComando">Indica si el mensaje modifica estado.</param>
    private sealed record DatosManejador(
        Type TipoManejador,
        MethodInfo Metodo,
        string NombreActividad,
        string NombreMensaje,
        bool EsComando)
    {
        /// <summary>Construye los metadatos a partir de los tipos implicados.</summary>
        /// <param name="tipoManejadorAbierto">Interfaz genérica abierta del manejador.</param>
        /// <param name="tipoMensaje">Tipo del comando o consulta.</param>
        /// <param name="tipoResultado">Tipo del resultado.</param>
        public static DatosManejador Crear(Type tipoManejadorAbierto, Type tipoMensaje, Type tipoResultado)
        {
            var tipoManejador = tipoManejadorAbierto.MakeGenericType(tipoMensaje, tipoResultado);
            var metodo = tipoManejador.GetMethod("ManejarAsync", BindingFlags.Public | BindingFlags.Instance)
                ?? throw new InvalidOperationException($"El manejador '{tipoManejador.Name}' no expone 'ManejarAsync'.");

            var esComando = tipoManejadorAbierto == typeof(IManejadorComando<,>);

            return new DatosManejador(
                tipoManejador,
                metodo,
                $"cqrs {tipoMensaje.Name}",
                tipoMensaje.Name,
                esComando);
        }
    }
}
