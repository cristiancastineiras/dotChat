namespace Chat.Aplicacion.Cqrs;

/// <summary>
/// Marca una operación que modifica el estado del sistema.
/// </summary>
/// <typeparam name="TResultado">Tipo devuelto por el manejador.</typeparam>
public interface IComando<TResultado>;

/// <summary>
/// Marca una operación de solo lectura.
/// </summary>
/// <typeparam name="TResultado">Tipo devuelto por el manejador.</typeparam>
public interface IConsulta<TResultado>;

/// <summary>Manejador de un comando concreto.</summary>
/// <typeparam name="TComando">Tipo del comando.</typeparam>
/// <typeparam name="TResultado">Tipo del resultado.</typeparam>
public interface IManejadorComando<in TComando, TResultado>
    where TComando : IComando<TResultado>
{
    /// <summary>Ejecuta el comando.</summary>
    /// <param name="comando">Datos de la operación.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    Task<TResultado> ManejarAsync(TComando comando, CancellationToken cancelacion = default);
}

/// <summary>Manejador de una consulta concreta.</summary>
/// <typeparam name="TConsulta">Tipo de la consulta.</typeparam>
/// <typeparam name="TResultado">Tipo del resultado.</typeparam>
public interface IManejadorConsulta<in TConsulta, TResultado>
    where TConsulta : IConsulta<TResultado>
{
    /// <summary>Ejecuta la consulta.</summary>
    /// <param name="consulta">Criterios de búsqueda.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    Task<TResultado> ManejarAsync(TConsulta consulta, CancellationToken cancelacion = default);
}

/// <summary>
/// Punto único de entrada para ejecutar comandos y consultas. Es un despachador
/// deliberadamente mínimo: resuelve el manejador del contenedor y lo invoca,
/// sin canalizaciones ni reflexión sobre ensamblados en tiempo de ejecución.
/// </summary>
public interface IDespachador
{
    /// <summary>Ejecuta un comando y devuelve su resultado.</summary>
    /// <typeparam name="TResultado">Tipo del resultado.</typeparam>
    /// <param name="comando">Comando a ejecutar.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    Task<TResultado> EjecutarAsync<TResultado>(IComando<TResultado> comando, CancellationToken cancelacion = default);

    /// <summary>Ejecuta una consulta y devuelve su resultado.</summary>
    /// <typeparam name="TResultado">Tipo del resultado.</typeparam>
    /// <param name="consulta">Consulta a ejecutar.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    Task<TResultado> ConsultarAsync<TResultado>(IConsulta<TResultado> consulta, CancellationToken cancelacion = default);
}
