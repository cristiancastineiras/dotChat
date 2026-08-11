namespace Chat.Aplicacion.Abstracciones;

/// <summary>
/// Limita cuántos mensajes por minuto puede publicar un usuario.
/// </summary>
/// <remarks>
/// <para>
/// El middleware de limitación de ASP.NET Core solo cubre peticiones HTTP; las
/// invocaciones dentro de una conexión de SignalR ya establecida no pasan por él y
/// necesitan este control propio.
/// </para>
/// <para>
/// El cupo es del usuario, no de la conexión ni del nodo. Si cada réplica llevara su
/// propia cuenta, bastaría con abrir varias conexiones para multiplicar el límite por
/// el número de instancias.
/// </para>
/// </remarks>
public interface ILimitadorEnvios
{
    /// <summary>Intenta consumir un permiso de envío para el usuario indicado.</summary>
    /// <param name="usuarioId">Usuario que envía.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    /// <returns><c>true</c> si el envío está permitido.</returns>
    Task<bool> IntentarConsumirAsync(Guid usuarioId, CancellationToken cancelacion = default);
}
