namespace Chat.Infraestructura.Presencia;

/// <summary>
/// Nombres de las claves con las que el registro de conexiones guarda su estado en
/// Valkey, agrupados aquí para que el reparto quede a la vista de un vistazo.
/// </summary>
/// <remarks>
/// <para>El estado se reparte en cinco estructuras, cada una con un porqué:</para>
/// <list type="bullet">
///   <item><description>
///     <c>conexiones</c> — tabla de conexión a sus datos. Es la lista que ve la
///     consola de administración, y su tamaño es el número de conexiones abiertas.
///   </description></item>
///   <item><description>
///     <c>usuario:{id}</c> — conjunto de conexiones de un usuario. Su cardinalidad es
///     lo que decide si alguien acaba de conectarse o de desconectarse del todo.
///   </description></item>
///   <item><description>
///     <c>replica:{id}</c> — conjunto de conexiones que atiende una réplica. Es lo que
///     permite limpiar de golpe lo que deja atrás una que se cae.
///   </description></item>
///   <item><description>
///     <c>conectados</c> — conjunto de usuarios en línea. Evita tener que recorrer
///     todos los conjuntos de usuario para contar o para comprobar una presencia.
///   </description></item>
///   <item><description>
///     <c>presencia</c> — tabla de usuario a su último estado conocido, que es lo que
///     sostiene el «visto por última vez a las…».
///   </description></item>
/// </list>
/// </remarks>
internal static class ClavesPresencia
{
    /// <summary>Separador de los campos dentro de un valor. No aparece en ningún dato de usuario.</summary>
    internal const char Separador = '\u001F';

    /// <summary>Tabla de conexiones activas.</summary>
    internal static string Conexiones(string prefijo) => $"{prefijo}presencia:conexiones";

    /// <summary>Conjunto de conexiones de un usuario.</summary>
    internal static string ConexionesDeUsuario(string prefijo, Guid usuarioId)
        => $"{prefijo}presencia:usuario:{usuarioId:N}";

    /// <summary>Conjunto de conexiones atendidas por una réplica.</summary>
    internal static string ConexionesDeReplica(string prefijo, string replicaId)
        => $"{prefijo}presencia:replica:{replicaId}";

    /// <summary>Conjunto de salas a las que está suscrita una conexión.</summary>
    internal static string SalasDeConexion(string prefijo, string conexionId)
        => $"{prefijo}presencia:salas:{conexionId}";

    /// <summary>Conjunto de usuarios con al menos una conexión abierta.</summary>
    internal static string Conectados(string prefijo) => $"{prefijo}presencia:conectados";

    /// <summary>Tabla de presencia por usuario.</summary>
    internal static string Presencia(string prefijo) => $"{prefijo}presencia:estado";

    /// <summary>Conjunto ordenado de réplicas vivas, puntuado por su última señal.</summary>
    internal static string Replicas(string prefijo) => $"{prefijo}presencia:replicas";
}
