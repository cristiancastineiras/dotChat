using Chat.Dominio.Entidades;

namespace Chat.Aplicacion.Dtos;

/// <summary>
/// Archivo adjunto a un mensaje, tal como se anuncia a los clientes.
/// Solo viajan los metadatos: los bytes se piden aparte, y únicamente si el cliente
/// va a pintarlos o el usuario decide descargarlos.
/// </summary>
/// <param name="Id">Identificador del adjunto, con el que se descarga.</param>
/// <param name="NombreArchivo">Nombre saneado del fichero.</param>
/// <param name="TipoMime">Tipo MIME del contenido almacenado.</param>
/// <param name="Tipo">Naturaleza del contenido: imagen o archivo genérico.</param>
/// <param name="TamanoBytes">Tamaño del contenido en claro, en bytes.</param>
/// <param name="Ancho">Anchura en píxeles; solo en las imágenes.</param>
/// <param name="Alto">Altura en píxeles; solo en las imágenes.</param>
public sealed record AdjuntoDto(
    Guid Id,
    string NombreArchivo,
    string TipoMime,
    TipoAdjunto Tipo,
    long TamanoBytes,
    int? Ancho = null,
    int? Alto = null)
{
    /// <summary>Indica si el cliente puede dibujar el contenido en la consola.</summary>
    public bool EsImagen => Tipo == TipoAdjunto.Imagen;
}

/// <summary>
/// Mensaje ya descifrado, listo para mostrarse al usuario.
/// El texto solo se descifra en memoria y para destinatarios autorizados.
/// </summary>
/// <param name="Id">Identificador del mensaje.</param>
/// <param name="SalaId">Sala a la que pertenece.</param>
/// <param name="SalaNombre">Nombre de la sala.</param>
/// <param name="UsuarioId">Autor del mensaje.</param>
/// <param name="NombreUsuario">Nombre del autor.</param>
/// <param name="Texto">Contenido en claro; vacío si el mensaje es solo una imagen.</param>
/// <param name="FechaEnvio">Fecha UTC de envío.</param>
/// <param name="Adjunto">Imagen adjunta, si el mensaje lleva una.</param>
public sealed record MensajeDto(
    Guid Id,
    Guid SalaId,
    string SalaNombre,
    Guid UsuarioId,
    string NombreUsuario,
    string Texto,
    DateTimeOffset FechaEnvio,
    AdjuntoDto? Adjunto = null);

/// <summary>Datos de entrada para publicar un mensaje.</summary>
/// <param name="SalaId">Sala destino.</param>
/// <param name="Texto">
/// Contenido en claro. Puede ir vacío si el mensaje lleva una imagen, en cuyo caso
/// actúa como pie de foto.
/// </param>
/// <param name="IdentificadorEnvio">
/// Identificador único generado por el cliente. Permite descartar reenvíos duplicados
/// (protección básica contra repetición) y hacer la operación idempotente.
/// </param>
/// <param name="AdjuntoId">
/// Archivo previamente subido que se publica con el mensaje. Nulo en los mensajes de
/// solo texto.
/// </param>
public sealed record SolicitudEnviarMensajeDto(
    Guid SalaId,
    string Texto,
    Guid IdentificadorEnvio,
    Guid? AdjuntoId = null);

/// <summary>
/// Contenido descifrado de un adjunto, listo para entregarse al cliente.
/// </summary>
/// <remarks>
/// Lleva un flujo y no un array: el contenido va del almacén de objetos a la respuesta
/// HTTP sin materializarse en la memoria del servidor. Quien lo recibe debe liberarlo.
/// </remarks>
/// <param name="Contenido">Flujo con el contenido en claro.</param>
/// <param name="TipoMime">Tipo MIME con el que anunciarlo.</param>
/// <param name="NombreArchivo">Nombre saneado del fichero.</param>
/// <param name="TamanoBytes">Tamaño en claro, en bytes.</param>
public sealed record ContenidoAdjuntoDto(
    Stream Contenido,
    string TipoMime,
    string NombreArchivo,
    long TamanoBytes) : IAsyncDisposable
{
    /// <inheritdoc />
    public ValueTask DisposeAsync() => Contenido.DisposeAsync();
}
