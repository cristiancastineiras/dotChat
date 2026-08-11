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
    /// Obtiene el último mensaje de cada una de las salas indicadas, para la vista de
    /// lista de conversaciones.
    /// </summary>
    /// <remarks>
    /// Se resuelve en una sola consulta y no en una por sala: la lista de chats se pinta
    /// en cada arranque del cliente y con veinte conversaciones serían veinte viajes a
    /// la base de datos para mostrar veinte líneas.
    /// </remarks>
    /// <param name="salaIds">Salas de las que se quiere el último mensaje.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    /// <returns>Diccionario de sala a su último mensaje; las salas vacías no aparecen.</returns>
    Task<IReadOnlyDictionary<Guid, UltimoMensajeSala>> ObtenerUltimosPorSalaAsync(
        IReadOnlyCollection<Guid> salaIds,
        CancellationToken cancelacion = default);

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

/// <summary>
/// Resumen del último mensaje de una sala, tal como lo necesita la lista de
/// conversaciones: lo justo para pintar una línea de previsualización.
/// </summary>
/// <param name="SalaId">Sala a la que pertenece.</param>
/// <param name="AutorId">Autor del mensaje.</param>
/// <param name="NombreAutor">Nombre del autor.</param>
/// <param name="TextoCifrado">Texto cifrado; nulo si el mensaje era solo un archivo.</param>
/// <param name="FechaEnvio">Fecha UTC de envío.</param>
/// <param name="NombreAdjunto">Nombre del archivo adjunto, si lo llevaba.</param>
/// <param name="TipoAdjunto">Naturaleza del adjunto, si lo llevaba.</param>
public sealed record UltimoMensajeSala(
    Guid SalaId,
    Guid AutorId,
    string? NombreAutor,
    string? TextoCifrado,
    DateTimeOffset FechaEnvio,
    string? NombreAdjunto,
    Entidades.TipoAdjunto? TipoAdjunto);
