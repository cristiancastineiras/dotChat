using Chat.Dominio.Entidades;

namespace Chat.Aplicacion.Dtos;

/// <summary>Proyección pública de una sala.</summary>
/// <param name="Id">Identificador de la sala.</param>
/// <param name="Nombre">
/// Nombre a mostrar. En una conversación directa es el nombre del interlocutor,
/// calculado desde el punto de vista de quien consulta.
/// </param>
/// <param name="Descripcion">Descripción opcional.</param>
/// <param name="Tipo">Naturaleza de la sala: pública, privada o directa.</param>
/// <param name="FechaCreacion">Fecha UTC de creación.</param>
/// <param name="FechaUltimaActividad">Fecha UTC del último mensaje; nula si no tiene ninguno.</param>
/// <param name="TotalMiembros">Número de miembros actuales.</param>
/// <param name="EsMiembro">Indica si quien consulta pertenece a la sala.</param>
/// <param name="MensajesSinLeer">Mensajes ajenos posteriores a la última lectura de quien consulta.</param>
public sealed record SalaDto(
    Guid Id,
    string Nombre,
    string? Descripcion,
    TipoSala Tipo,
    DateTimeOffset FechaCreacion,
    DateTimeOffset? FechaUltimaActividad,
    int TotalMiembros,
    bool EsMiembro = false,
    int MensajesSinLeer = 0)
{
    /// <summary>Indica si la sala es una conversación directa entre dos personas.</summary>
    public bool EsDirecta => Tipo == TipoSala.Directa;
}

/// <summary>Datos de entrada para crear una sala.</summary>
/// <param name="Nombre">Nombre único de la sala.</param>
/// <param name="Descripcion">Descripción opcional.</param>
/// <param name="Privada">
/// Si es cierto, la sala solo será visible para sus miembros y habrá que invitar
/// a quien deba entrar.
/// </param>
public sealed record SolicitudCrearSalaDto(string Nombre, string? Descripcion, bool Privada = false);

/// <summary>Datos de entrada para abrir una conversación directa.</summary>
/// <param name="NombreUsuario">Nombre del interlocutor; alternativa a indicar su identificador.</param>
/// <param name="UsuarioId">Identificador del interlocutor; tiene prioridad sobre el nombre.</param>
public sealed record SolicitudConversacionDirectaDto(string? NombreUsuario = null, Guid? UsuarioId = null);

/// <summary>Datos de entrada para invitar a alguien a una sala privada.</summary>
/// <param name="NombreUsuario">Nombre del usuario invitado.</param>
public sealed record SolicitudInvitarDto(string NombreUsuario);

/// <summary>Miembro de una sala junto con su estado de conexión.</summary>
/// <param name="UsuarioId">Identificador del usuario.</param>
/// <param name="NombreUsuario">Nombre del usuario.</param>
/// <param name="FechaUnion">Fecha UTC en la que entró en la sala.</param>
/// <param name="EnLinea">Indica si tiene alguna conexión abierta en este momento.</param>
/// <param name="EsCreador">Indica si es quien creó la sala.</param>
public sealed record MiembroSalaDto(
    Guid UsuarioId,
    string NombreUsuario,
    DateTimeOffset FechaUnion,
    bool EnLinea,
    bool EsCreador);
