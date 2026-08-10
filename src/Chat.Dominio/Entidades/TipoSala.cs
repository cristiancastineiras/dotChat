namespace Chat.Dominio.Entidades;

/// <summary>
/// Naturaleza de una sala. Determina quién puede verla en el catálogo y quién
/// puede unirse a ella por su cuenta.
/// </summary>
public enum TipoSala
{
    /// <summary>
    /// Sala abierta: aparece en el catálogo y cualquier usuario autenticado puede unirse.
    /// </summary>
    Publica = 0,

    /// <summary>
    /// Sala restringida: solo la ven sus miembros y hay que ser invitado para entrar.
    /// </summary>
    Privada = 1,

    /// <summary>
    /// Conversación directa entre exactamente dos personas. No aparece nunca en el
    /// catálogo y no admite nuevos miembros.
    /// </summary>
    Directa = 2
}
