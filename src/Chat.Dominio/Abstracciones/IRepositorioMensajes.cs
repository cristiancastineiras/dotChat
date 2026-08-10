using Chat.Dominio.Entidades;

namespace Chat.Dominio.Abstracciones;

/// <summary>Acceso a datos de la entidad <see cref="Mensaje"/>.</summary>
public interface IRepositorioMensajes
{
    /// <summary>Añade un mensaje nuevo al contexto.</summary>
    /// <param name="mensaje">Mensaje a añadir.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    Task AgregarAsync(Mensaje mensaje, CancellationToken cancelacion = default);

    /// <summary>
    /// Obtiene los mensajes más recientes de una sala, ordenados de más antiguo a más nuevo
    /// dentro de la página devuelta.
    /// </summary>
    /// <param name="salaId">Identificador de la sala.</param>
    /// <param name="cantidad">Número máximo de mensajes a devolver.</param>
    /// <param name="anteriorA">Devuelve solo mensajes anteriores a esta fecha; nulo para los últimos.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    Task<IReadOnlyList<Mensaje>> ObtenerRecientesAsync(
        Guid salaId,
        int cantidad,
        DateTimeOffset? anteriorA,
        CancellationToken cancelacion = default);

    /// <summary>Cuenta los mensajes almacenados, opcionalmente filtrando por sala.</summary>
    /// <param name="salaId">Sala a filtrar; nulo para contar todos.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    Task<int> ContarAsync(Guid? salaId, CancellationToken cancelacion = default);

    /// <summary>
    /// Cuenta, para cada sala del usuario, los mensajes ajenos posteriores a su marca
    /// de lectura. Se resuelve en una sola consulta para no multiplicar viajes a la
    /// base de datos al pintar la lista de salas.
    /// </summary>
    /// <param name="usuarioId">Usuario para el que se calculan los pendientes.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    /// <returns>Diccionario de sala a número de mensajes sin leer; las salas al día no aparecen.</returns>
    Task<IReadOnlyDictionary<Guid, int>> ContarNoLeidosPorSalaAsync(
        Guid usuarioId,
        CancellationToken cancelacion = default);
}
