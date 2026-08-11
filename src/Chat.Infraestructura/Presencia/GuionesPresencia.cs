namespace Chat.Infraestructura.Presencia;

/// <summary>
/// Guiones Lua que ejecutan de forma atómica los cambios de estado de la presencia.
/// </summary>
/// <remarks>
/// <para>
/// Registrar o cerrar una conexión toca cuatro o cinco estructuras a la vez, y de la
/// última de ellas sale la respuesta a la única pregunta que importa: si el usuario
/// acaba de conectarse o de desconectarse. Hacerlo con llamadas sueltas desde el
/// servidor abriría una ventana en la que dos réplicas leen «cero conexiones» a la vez
/// y ambas anuncian la desconexión del mismo usuario.
/// </para>
/// <para>
/// Valkey ejecuta cada guion sin intercalar nada, así que el bloque entero se comporta
/// como una sola operación y la respuesta es siempre coherente con lo que quedó escrito.
/// </para>
/// <para>
/// Los nombres de clave se componen dentro del guion a partir de un prefijo. Eso obliga
/// a que todas las claves vivan en el mismo nodo, algo que se cumple en un Valkey
/// normal; en un despliegue en clúster habría que asignarles una ranura común con
/// llaves de hash.
/// </para>
/// </remarks>
internal static class GuionesPresencia
{
    /// <summary>
    /// Registra una conexión. Devuelve 1 si el usuario acaba de pasar a estar en línea.
    /// </summary>
    /// <remarks>
    /// ARGV: prefijo, conexionId, datosConexion, usuarioId, presencia, replicaId.
    /// </remarks>
    internal const string Registrar =
        """
        local p = ARGV[1]
        local conexionId = ARGV[2]
        local usuarioId = ARGV[4]

        redis.call('HSET', p .. 'presencia:conexiones', conexionId, ARGV[3])
        redis.call('SADD', p .. 'presencia:usuario:' .. usuarioId, conexionId)
        redis.call('SADD', p .. 'presencia:replica:' .. ARGV[6], conexionId)
        redis.call('HSET', p .. 'presencia:estado', usuarioId, ARGV[5])

        local abiertas = redis.call('SCARD', p .. 'presencia:usuario:' .. usuarioId)

        if abiertas == 1 then
            redis.call('SADD', p .. 'presencia:conectados', usuarioId)
            return 1
        end

        return 0
        """;

    /// <summary>
    /// Cierra una conexión. Devuelve el usuario, su nombre y cuántas conexiones le
    /// quedan, o nada si la conexión no constaba.
    /// </summary>
    /// <remarks>
    /// ARGV: prefijo, conexionId, fechaDesconexion.
    /// </remarks>
    internal const string Eliminar =
        """
        local p = ARGV[1]
        local conexionId = ARGV[2]
        local datos = redis.call('HGET', p .. 'presencia:conexiones', conexionId)

        if not datos then
            return nil
        end

        local campos = {}
        for campo in string.gmatch(datos, '([^\31]+)') do
            campos[#campos + 1] = campo
        end

        local usuarioId = campos[1]
        local nombre = campos[2]
        local replicaId = campos[4]

        redis.call('HDEL', p .. 'presencia:conexiones', conexionId)
        redis.call('SREM', p .. 'presencia:usuario:' .. usuarioId, conexionId)
        redis.call('SREM', p .. 'presencia:replica:' .. replicaId, conexionId)
        redis.call('DEL', p .. 'presencia:salas:' .. conexionId)

        local abiertas = redis.call('SCARD', p .. 'presencia:usuario:' .. usuarioId)

        if abiertas == 0 then
            redis.call('SREM', p .. 'presencia:conectados', usuarioId)
        end

        redis.call('HSET', p .. 'presencia:estado', usuarioId,
            nombre .. '\31' .. ARGV[3] .. '\31' .. tostring(abiertas))

        return { usuarioId, nombre, tostring(abiertas) }
        """;

    /// <summary>
    /// Anuncia que esta réplica sigue viva y retira las conexiones de las que hayan
    /// dejado de anunciarse. Devuelve los usuarios que han quedado desconectados.
    /// </summary>
    /// <remarks>
    /// ARGV: prefijo, replicaId, ahoraEnTicks, limiteEnTicks.
    /// </remarks>
    internal const string LatirYLimpiar =
        """
        local p = ARGV[1]
        local ahora = ARGV[3]

        redis.call('ZADD', p .. 'presencia:replicas', ahora, ARGV[2])

        local muertas = redis.call('ZRANGEBYSCORE', p .. 'presencia:replicas', '-inf', ARGV[4])
        local desconectados = {}

        for _, replicaId in ipairs(muertas) do
            local clave = p .. 'presencia:replica:' .. replicaId
            local conexiones = redis.call('SMEMBERS', clave)

            for _, conexionId in ipairs(conexiones) do
                local datos = redis.call('HGET', p .. 'presencia:conexiones', conexionId)

                if datos then
                    local campos = {}
                    for campo in string.gmatch(datos, '([^\31]+)') do
                        campos[#campos + 1] = campo
                    end

                    local usuarioId = campos[1]
                    local nombre = campos[2]

                    redis.call('HDEL', p .. 'presencia:conexiones', conexionId)
                    redis.call('SREM', p .. 'presencia:usuario:' .. usuarioId, conexionId)
                    redis.call('DEL', p .. 'presencia:salas:' .. conexionId)

                    if redis.call('SCARD', p .. 'presencia:usuario:' .. usuarioId) == 0 then
                        redis.call('SREM', p .. 'presencia:conectados', usuarioId)
                        redis.call('HSET', p .. 'presencia:estado', usuarioId,
                            nombre .. '\31' .. ahora .. '\31' .. '0')
                        desconectados[#desconectados + 1] = usuarioId .. '\31' .. nombre
                    end
                end
            end

            redis.call('DEL', clave)
            redis.call('ZREM', p .. 'presencia:replicas', replicaId)
        end

        return desconectados
        """;
}
