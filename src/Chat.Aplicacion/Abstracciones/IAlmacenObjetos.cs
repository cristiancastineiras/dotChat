namespace Chat.Aplicacion.Abstracciones;

/// <summary>
/// Almacén de los contenidos binarios que acompañan a los mensajes.
/// </summary>
/// <remarks>
/// <para>
/// Los archivos no viven en la base de datos. Una fila de PostgreSQL con veinte megas
/// dentro se lleva por delante las copias de seguridad, la replicación y la caché de
/// páginas del motor, y obliga a que cada réplica del servidor tenga acceso al mismo
/// disco. Un almacén de objetos está pensado justamente para esto: es compartido,
/// crece aparte y sirve el contenido en flujo.
/// </para>
/// <para>
/// La interfaz es deliberadamente pequeña y no menciona MinIO ni S3. La implementación
/// habla el protocolo de S3, así que vale igual para MinIO en local que para un
/// almacén gestionado en producción.
/// </para>
/// </remarks>
public interface IAlmacenObjetos
{
    /// <summary>
    /// Prepara el almacén: crea el contenedor si no existe y comprueba que se puede
    /// escribir en él. Se invoca una vez al arrancar.
    /// </summary>
    /// <param name="cancelacion">Token de cancelación.</param>
    Task PrepararAsync(CancellationToken cancelacion = default);

    /// <summary>Guarda un contenido bajo la clave indicada.</summary>
    /// <param name="clave">Ruta lógica del objeto dentro del contenedor.</param>
    /// <param name="contenido">Flujo legible con el contenido a guardar.</param>
    /// <param name="tamano">Tamaño exacto del contenido, en bytes.</param>
    /// <param name="tipoMime">Tipo de contenido que se anota junto al objeto.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    Task GuardarAsync(string clave, Stream contenido, long tamano, string tipoMime, CancellationToken cancelacion = default);

    /// <summary>Abre un objeto para leerlo.</summary>
    /// <param name="clave">Clave del objeto.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    /// <returns>Flujo de solo lectura; quien lo recibe se encarga de liberarlo.</returns>
    /// <exception cref="Chat.Dominio.Excepciones.ExcepcionNoEncontrado">Si el objeto no existe.</exception>
    Task<Stream> AbrirAsync(string clave, CancellationToken cancelacion = default);

    /// <summary>Borra un objeto. No falla si ya no estaba.</summary>
    /// <param name="clave">Clave del objeto.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    Task EliminarAsync(string clave, CancellationToken cancelacion = default);

    /// <summary>Borra varios objetos de una vez.</summary>
    /// <param name="claves">Claves de los objetos.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    Task EliminarVariosAsync(IReadOnlyCollection<string> claves, CancellationToken cancelacion = default);

    /// <summary>Comprueba que el almacén responde. Se usa en la sonda de salud.</summary>
    /// <param name="cancelacion">Token de cancelación.</param>
    Task<bool> RespondeAsync(CancellationToken cancelacion = default);
}
