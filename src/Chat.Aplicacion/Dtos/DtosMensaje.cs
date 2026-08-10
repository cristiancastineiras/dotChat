namespace Chat.Aplicacion.Dtos;

/// <summary>
/// Mensaje ya descifrado, listo para mostrarse al usuario.
/// El texto solo se descifra en memoria y para destinatarios autorizados.
/// </summary>
/// <param name="Id">Identificador del mensaje.</param>
/// <param name="SalaId">Sala a la que pertenece.</param>
/// <param name="SalaNombre">Nombre de la sala.</param>
/// <param name="UsuarioId">Autor del mensaje.</param>
/// <param name="NombreUsuario">Nombre del autor.</param>
/// <param name="Texto">Contenido en claro.</param>
/// <param name="FechaEnvio">Fecha UTC de envío.</param>
public sealed record MensajeDto(
    Guid Id,
    Guid SalaId,
    string SalaNombre,
    Guid UsuarioId,
    string NombreUsuario,
    string Texto,
    DateTimeOffset FechaEnvio);

/// <summary>Datos de entrada para publicar un mensaje.</summary>
/// <param name="SalaId">Sala destino.</param>
/// <param name="Texto">Contenido en claro.</param>
/// <param name="IdentificadorEnvio">
/// Identificador único generado por el cliente. Permite descartar reenvíos duplicados
/// (protección básica contra repetición) y hacer la operación idempotente.
/// </param>
public sealed record SolicitudEnviarMensajeDto(Guid SalaId, string Texto, Guid IdentificadorEnvio);
