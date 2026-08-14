using Chat.Aplicacion.Dtos;
using Chat.Dominio.Entidades;

namespace Chat.Aplicacion.Mapeos;

/// <summary>
/// Conversión de entidades de dominio a DTO. Se hace a mano y en un único lugar:
/// es explícito, sin reflexión, y garantiza que nunca se filtren campos sensibles.
/// </summary>
public static class Proyecciones
{
    /// <summary>Nombre que se muestra cuando no se conoce al autor de un mensaje.</summary>
    public const string NombreDesconocido = "(desconocido)";

    /// <summary>Proyecta un usuario a su representación pública.</summary>
    /// <param name="usuario">Entidad de origen.</param>
    /// <param name="enLinea">Estado de conexión resuelto por el registro de presencia.</param>
    public static UsuarioDto ADto(this Usuario usuario, bool enLinea = false) => new(
        usuario.Id,
        usuario.UserName ?? string.Empty,
        usuario.Email ?? string.Empty,
        usuario.FechaCreacion,
        usuario.FechaUltimoAcceso,
        usuario.Activo,
        enLinea,
        usuario.TieneAvatar,
        usuario.AvatarActualizado);

    /// <summary>Proyecta un usuario a la vista que tiene de sí mismo.</summary>
    /// <param name="usuario">Entidad de origen.</param>
    /// <param name="esAdministrador">Rol resuelto desde el almacén de identidad.</param>
    public static PerfilDto APerfil(this Usuario usuario, bool esAdministrador)
    {
        ArgumentNullException.ThrowIfNull(usuario);

        return new PerfilDto(
            usuario.Id,
            usuario.UserName ?? string.Empty,
            usuario.Email ?? string.Empty,
            usuario.FechaCreacion,
            usuario.FechaUltimoAcceso,
            esAdministrador,
            usuario.TieneAvatar,
            usuario.AvatarActualizado);
    }

    /// <summary>Proyecta una sala a su representación pública.</summary>
    /// <param name="sala">Entidad de origen.</param>
    /// <param name="totalMiembros">Número de miembros calculado por el repositorio.</param>
    /// <param name="nombreVisible">
    /// Nombre a mostrar cuando no coincide con el almacenado; es el caso de las
    /// conversaciones directas, que se presentan con el nombre del interlocutor.
    /// </param>
    /// <param name="esMiembro">Indica si quien consulta pertenece a la sala.</param>
    /// <param name="mensajesSinLeer">Mensajes pendientes para quien consulta.</param>
    /// <param name="ultimoMensaje">Previsualización del último mensaje.</param>
    /// <param name="interlocutorEnLinea">Presencia del interlocutor en una conversación directa.</param>
    public static SalaDto ADto(
        this Sala sala,
        int totalMiembros,
        string? nombreVisible = null,
        bool esMiembro = false,
        int mensajesSinLeer = 0,
        ResumenMensajeDto? ultimoMensaje = null,
        bool? interlocutorEnLinea = null) => new(
        sala.Id,
        nombreVisible ?? sala.Nombre,
        sala.Descripcion,
        sala.Tipo,
        sala.FechaCreacion,
        sala.FechaUltimaActividad,
        totalMiembros,
        esMiembro,
        mensajesSinLeer,
        ultimoMensaje,
        interlocutorEnLinea);

    /// <summary>Proyecta la membresía de una sala junto con la presencia del usuario.</summary>
    /// <param name="miembro">Entidad de origen, con su usuario cargado.</param>
    /// <param name="enLinea">Estado de conexión del usuario.</param>
    /// <param name="esCreador">Indica si el usuario creó la sala.</param>
    public static MiembroSalaDto ADto(this MiembroSala miembro, bool enLinea, bool esCreador) => new(
        miembro.UsuarioId,
        miembro.Usuario?.UserName ?? NombreDesconocido,
        miembro.FechaUnion,
        enLinea,
        esCreador);

    /// <summary>Proyecta un mensaje ya descifrado a su representación pública.</summary>
    /// <param name="mensaje">Entidad de origen, con su adjunto cargado si lo tiene.</param>
    /// <param name="texto">Texto en claro obtenido del cifrador; vacío si no hay.</param>
    /// <param name="nombreSala">Nombre de la sala.</param>
    /// <param name="nombreUsuario">Nombre del autor.</param>
    public static MensajeDto ADto(this Mensaje mensaje, string texto, string nombreSala, string nombreUsuario)
    {
        ArgumentNullException.ThrowIfNull(mensaje);

        return new MensajeDto(
            mensaje.Id,
            mensaje.SalaId,
            nombreSala,
            mensaje.UsuarioId,
            nombreUsuario,
            texto,
            mensaje.FechaEnvio,
            mensaje.Adjunto?.ADto());
    }

    /// <summary>Proyecta los metadatos de un adjunto. El binario nunca entra en el DTO.</summary>
    /// <param name="adjunto">Entidad de origen.</param>
    public static AdjuntoDto ADto(this Adjunto adjunto)
    {
        ArgumentNullException.ThrowIfNull(adjunto);

        return new AdjuntoDto(
            adjunto.Id,
            adjunto.NombreArchivo,
            adjunto.TipoMime,
            adjunto.Tipo,
            adjunto.TamanoBytes,
            adjunto.Ancho,
            adjunto.Alto,
            adjunto.DuracionMs);
    }

    /// <summary>
    /// Nombre de sala que acompaña a un mensaje. En una conversación directa el
    /// nombre almacenado es un identificador interno que no debe salir del servidor,
    /// así que se sustituye por el del autor: es como el destinatario reconoce la
    /// conversación, y el emisor localiza la suya por identificador.
    /// </summary>
    /// <param name="sala">Sala en la que se publicó el mensaje.</param>
    /// <param name="nombreAutor">Nombre de quien lo escribió.</param>
    public static string NombreSalaEnMensaje(Sala sala, string nombreAutor)
    {
        ArgumentNullException.ThrowIfNull(sala);
        return sala.Tipo == TipoSala.Directa ? nombreAutor : sala.Nombre;
    }

    /// <summary>
    /// Calcula el nombre con el que se presenta una sala a un usuario concreto:
    /// en una conversación directa es el nombre de la otra persona; en el resto,
    /// el nombre propio de la sala.
    /// </summary>
    /// <param name="sala">Sala con sus miembros y usuarios cargados.</param>
    /// <param name="usuarioId">Usuario desde cuyo punto de vista se calcula.</param>
    public static string NombreVisiblePara(this Sala sala, Guid usuarioId)
    {
        ArgumentNullException.ThrowIfNull(sala);

        if (sala.Tipo != TipoSala.Directa)
        {
            return sala.Nombre;
        }

        foreach (var miembro in sala.Miembros)
        {
            if (miembro.UsuarioId != usuarioId)
            {
                return miembro.Usuario?.UserName ?? NombreDesconocido;
            }
        }

        // Conversación consigo mismo o interlocutor eliminado.
        return NombreDesconocido;
    }
}
