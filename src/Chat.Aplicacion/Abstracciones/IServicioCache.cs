namespace Chat.Aplicacion.Abstracciones;

/// <summary>
/// Fachada sobre la caché (FusionCache) para que la capa de aplicación no dependa
/// del paquete concreto y las pruebas puedan sustituirla con facilidad.
/// </summary>
public interface IServicioCache
{
    /// <summary>Obtiene el valor de la caché o lo genera y almacena si no existe.</summary>
    /// <typeparam name="T">Tipo del valor cacheado.</typeparam>
    /// <param name="clave">Clave de caché.</param>
    /// <param name="generador">Función que produce el valor cuando no está en caché.</param>
    /// <param name="duracion">Duración de la entrada; nula para usar la duración por defecto.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    Task<T> ObtenerOCrearAsync<T>(
        string clave,
        Func<CancellationToken, Task<T>> generador,
        TimeSpan? duracion = null,
        CancellationToken cancelacion = default);

    /// <summary>Guarda o reemplaza un valor en la caché.</summary>
    /// <typeparam name="T">Tipo del valor.</typeparam>
    /// <param name="clave">Clave de caché.</param>
    /// <param name="valor">Valor a almacenar.</param>
    /// <param name="duracion">Duración de la entrada; nula para usar la duración por defecto.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    Task EstablecerAsync<T>(
        string clave,
        T valor,
        TimeSpan? duracion = null,
        CancellationToken cancelacion = default);

    /// <summary>Obtiene un valor si existe en caché.</summary>
    /// <typeparam name="T">Tipo del valor.</typeparam>
    /// <param name="clave">Clave de caché.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    /// <returns>El valor almacenado o el valor por defecto de <typeparamref name="T"/>.</returns>
    Task<T?> ObtenerAsync<T>(string clave, CancellationToken cancelacion = default);

    /// <summary>Invalida una entrada concreta.</summary>
    /// <param name="clave">Clave de caché.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    Task InvalidarAsync(string clave, CancellationToken cancelacion = default);

    /// <summary>Invalida todas las entradas asociadas a una etiqueta.</summary>
    /// <param name="etiqueta">Etiqueta lógica (por ejemplo, «salas»).</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    Task InvalidarPorEtiquetaAsync(string etiqueta, CancellationToken cancelacion = default);

    /// <summary>Vacía por completo la caché.</summary>
    /// <param name="cancelacion">Token de cancelación.</param>
    Task LimpiarTodoAsync(CancellationToken cancelacion = default);
}
