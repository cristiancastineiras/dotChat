namespace Chat.Dominio.Entidades;

/// <summary>
/// Naturaleza de un archivo adjunto. Decide cómo lo presenta el cliente: una imagen
/// se dibuja en la conversación y el resto se anuncia con su ficha para descargarlo.
/// </summary>
public enum TipoAdjunto
{
    /// <summary>Archivo genérico: se descarga, no se representa.</summary>
    Archivo = 0,

    /// <summary>Imagen validada y recodificada por el servidor.</summary>
    Imagen = 1,

    /// <summary>Nota de voz u otro audio grabado por el cliente.</summary>
    Audio = 2
}
