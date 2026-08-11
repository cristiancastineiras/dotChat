using Chat.Dominio.Entidades;

namespace Chat.Dominio.Abstracciones;

/// <summary>Acceso a datos de la entidad <see cref="Adjunto"/>.</summary>
public interface IRepositorioAdjuntos
{
    /// <summary>Añade la ficha de un adjunto nuevo al contexto.</summary>
    /// <param name="adjunto">Adjunto a añadir.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    Task AgregarAsync(Adjunto adjunto, CancellationToken cancelacion = default);

    /// <summary>Obtiene la ficha de un adjunto.</summary>
    /// <param name="adjuntoId">Identificador del adjunto.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    /// <returns>El adjunto, o <c>null</c> si no existe.</returns>
    Task<Adjunto?> ObtenerPorIdAsync(Guid adjuntoId, CancellationToken cancelacion = default);

    /// <summary>Indica si un adjunto ya se publicó en algún mensaje.</summary>
    /// <param name="adjuntoId">Identificador del adjunto.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    Task<bool> EstaPublicadoAsync(Guid adjuntoId, CancellationToken cancelacion = default);

    /// <summary>Cuenta los adjuntos almacenados.</summary>
    /// <param name="cancelacion">Token de cancelación.</param>
    Task<int> ContarAsync(CancellationToken cancelacion = default);

    /// <summary>Suma el espacio ocupado por todos los adjuntos, en bytes.</summary>
    /// <param name="cancelacion">Token de cancelación.</param>
    Task<long> SumarTamanoAsync(CancellationToken cancelacion = default);

    /// <summary>
    /// Elimina las fichas de los adjuntos subidos antes de la fecha indicada que nunca
    /// llegaron a publicarse, y devuelve las claves de sus objetos para poder borrarlos
    /// también del almacén.
    /// </summary>
    /// <remarks>
    /// Primero se leen las claves y después se borran las filas: si el proceso se
    /// interrumpe entre ambas cosas, lo peor que queda es un objeto sin ficha, que la
    /// siguiente pasada no vuelve a ver. Al revés quedaría una ficha sin contenido, que
    /// sí rompe una descarga.
    /// </remarks>
    /// <param name="anteriorA">Fecha límite: se purga lo subido antes de este instante.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    /// <returns>Claves de los objetos que han quedado sin ficha.</returns>
    Task<IReadOnlyList<string>> PurgarHuerfanosAsync(
        DateTimeOffset anteriorA,
        CancellationToken cancelacion = default);
}
