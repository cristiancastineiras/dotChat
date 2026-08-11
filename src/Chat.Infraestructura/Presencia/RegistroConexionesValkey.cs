using System.Globalization;
using Chat.Aplicacion.Abstracciones;
using Chat.Aplicacion.Dtos;
using Chat.Aplicacion.Opciones;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Chat.Infraestructura.Presencia;

/// <summary>
/// Registro de conexiones y presencia compartido por todas las réplicas, sostenido
/// sobre Valkey.
/// </summary>
/// <remarks>
/// <para>
/// Es la pieza que hace posible ejecutar más de una instancia del servidor. Cuando el
/// estado vivía en memoria, dos conexiones del mismo usuario repartidas entre dos
/// réplicas se contaban por separado: cerrar una anunciaba la desconexión mientras la
/// otra seguía abierta, y la consola de administración solo veía las conexiones del
/// nodo al que hubiera caído la petición.
/// </para>
/// <para>
/// Cada réplica se identifica y deja constancia de que sigue viva. Si una se cae de
/// golpe, sus conexiones nunca reciben el cierre ordenado; la limpieza periódica las
/// retira y anuncia las desconexiones que correspondan.
/// </para>
/// </remarks>
public sealed class RegistroConexionesValkey : IRegistroConexiones
{
    private readonly IConnectionMultiplexer _conexion;
    private readonly ILogger<RegistroConexionesValkey> _registro;
    private readonly string _prefijo;

    /// <summary>Identificador de esta réplica, estable mientras el proceso viva.</summary>
    private readonly string _replicaId;

    /// <summary>Crea el registro.</summary>
    /// <param name="conexion">Conexión compartida con Valkey.</param>
    /// <param name="opciones">Opciones de Valkey, de las que sale el prefijo de claves.</param>
    /// <param name="identidad">Identidad de esta réplica dentro del clúster.</param>
    /// <param name="registro">Registro estructurado.</param>
    public RegistroConexionesValkey(
        IConnectionMultiplexer conexion,
        IOptions<ValkeyOptions> opciones,
        IdentidadReplica identidad,
        ILogger<RegistroConexionesValkey> registro)
    {
        ArgumentNullException.ThrowIfNull(opciones);
        ArgumentNullException.ThrowIfNull(identidad);

        _conexion = conexion;
        _registro = registro;
        _prefijo = opciones.Value.PrefijoClaves();
        _replicaId = identidad.Id;
    }

    /// <summary>Base de datos de Valkey sobre la que se opera.</summary>
    private IDatabase Base => _conexion.GetDatabase();

    /// <inheritdoc />
    public async Task<bool> RegistrarAsync(
        string conexionId,
        Guid usuarioId,
        string nombreUsuario,
        DateTimeOffset fechaConexion,
        CancellationToken cancelacion = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(conexionId);

        var datos = Unir(
            usuarioId.ToString("N"),
            nombreUsuario,
            Marcar(fechaConexion),
            _replicaId);

        var presencia = Unir(nombreUsuario, Marcar(fechaConexion), "1");

        var resultado = await Base.ScriptEvaluateAsync(
            GuionesPresencia.Registrar,
            values:
            [
                _prefijo,
                conexionId,
                datos,
                usuarioId.ToString("N"),
                presencia,
                _replicaId
            ]).ConfigureAwait(false);

        return (int)resultado == 1;
    }

    /// <inheritdoc />
    public async Task<ConexionCerrada?> EliminarAsync(
        string conexionId,
        DateTimeOffset fechaDesconexion,
        CancellationToken cancelacion = default)
    {
        if (string.IsNullOrWhiteSpace(conexionId))
        {
            return null;
        }

        var resultado = await Base.ScriptEvaluateAsync(
            GuionesPresencia.Eliminar,
            values: [_prefijo, conexionId, Marcar(fechaDesconexion)]).ConfigureAwait(false);

        if (resultado.IsNull)
        {
            return null;
        }

        var campos = (RedisValue[]?)resultado;

        if (campos is not { Length: 3 })
        {
            return null;
        }

        var usuarioId = Guid.ParseExact((string)campos[0]!, "N");
        var nombre = (string)campos[1]!;
        var abiertas = int.Parse((string)campos[2]!, CultureInfo.InvariantCulture);

        return new ConexionCerrada(usuarioId, nombre, abiertas == 0);
    }

    /// <inheritdoc />
    public async Task AgregarSalaAsync(
        string conexionId,
        string nombreSala,
        CancellationToken cancelacion = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(conexionId);

        await Base.SetAddAsync(ClavesPresencia.SalasDeConexion(_prefijo, conexionId), nombreSala)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task QuitarSalaAsync(
        string conexionId,
        string nombreSala,
        CancellationToken cancelacion = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(conexionId);

        await Base.SetRemoveAsync(ClavesPresencia.SalasDeConexion(_prefijo, conexionId), nombreSala)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ConexionActivaDto>> ListarAsync(CancellationToken cancelacion = default)
    {
        var entradas = await Base.HashGetAllAsync(ClavesPresencia.Conexiones(_prefijo)).ConfigureAwait(false);

        if (entradas.Length == 0)
        {
            return [];
        }

        // Las salas de cada conexión se piden en una sola tanda: el cliente las envía
        // seguidas y espera las respuestas juntas, en lugar de una ida y vuelta por
        // conexión.
        var lote = Base.CreateBatch();
        var salas = new Task<RedisValue[]>[entradas.Length];

        for (var indice = 0; indice < entradas.Length; indice++)
        {
            salas[indice] = lote.SetMembersAsync(
                ClavesPresencia.SalasDeConexion(_prefijo, entradas[indice].Name!));
        }

        lote.Execute();
        await Task.WhenAll(salas).ConfigureAwait(false);

        var conexiones = new List<ConexionActivaDto>(entradas.Length);

        for (var indice = 0; indice < entradas.Length; indice++)
        {
            var campos = Separar((string)entradas[indice].Value!);

            if (campos.Length < 4)
            {
                continue;
            }

            conexiones.Add(new ConexionActivaDto(
                (string)entradas[indice].Name!,
                Guid.ParseExact(campos[0], "N"),
                campos[1],
                Desmarcar(campos[2]),
                [.. salas[indice].Result.Select(sala => (string)sala!).Order(StringComparer.OrdinalIgnoreCase)]));
        }

        return [.. conexiones.OrderBy(conexion => conexion.FechaConexion)];
    }

    /// <inheritdoc />
    public async Task<bool> EstaConectadoAsync(Guid usuarioId, CancellationToken cancelacion = default)
        => await Base.SetContainsAsync(ClavesPresencia.Conectados(_prefijo), usuarioId.ToString("N"))
            .ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<IReadOnlySet<Guid>> FiltrarConectadosAsync(
        IReadOnlyCollection<Guid> usuarioIds,
        CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(usuarioIds);

        if (usuarioIds.Count == 0)
        {
            return new HashSet<Guid>();
        }

        var candidatos = usuarioIds.Distinct().ToArray();
        var valores = candidatos.Select(id => (RedisValue)id.ToString("N")).ToArray();

        var pertenencias = await Base
            .SetContainsAsync(ClavesPresencia.Conectados(_prefijo), valores)
            .ConfigureAwait(false);

        var conectados = new HashSet<Guid>();

        for (var indice = 0; indice < candidatos.Length; indice++)
        {
            if (pertenencias[indice])
            {
                conectados.Add(candidatos[indice]);
            }
        }

        return conectados;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> ConexionesDeAsync(
        Guid usuarioId,
        CancellationToken cancelacion = default)
    {
        var miembros = await Base
            .SetMembersAsync(ClavesPresencia.ConexionesDeUsuario(_prefijo, usuarioId))
            .ConfigureAwait(false);

        return [.. miembros.Select(miembro => (string)miembro!)];
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PresenciaDto>> ListarPresenciaAsync(CancellationToken cancelacion = default)
    {
        var entradas = await Base.HashGetAllAsync(ClavesPresencia.Presencia(_prefijo)).ConfigureAwait(false);
        var presencias = new List<PresenciaDto>(entradas.Length);

        foreach (var entrada in entradas)
        {
            var campos = Separar((string)entrada.Value!);

            if (campos.Length < 3)
            {
                continue;
            }

            var abiertas = int.Parse(campos[2], CultureInfo.InvariantCulture);

            presencias.Add(new PresenciaDto(
                Guid.ParseExact((string)entrada.Name!, "N"),
                campos[0],
                abiertas > 0,
                Desmarcar(campos[1]),
                abiertas));
        }

        return
        [
            .. presencias
                .OrderByDescending(presencia => presencia.EnLinea)
                .ThenBy(presencia => presencia.NombreUsuario, StringComparer.OrdinalIgnoreCase)
        ];
    }

    /// <inheritdoc />
    public async Task<int> ContarConexionesAsync(CancellationToken cancelacion = default)
        => (int)await Base.HashLengthAsync(ClavesPresencia.Conexiones(_prefijo)).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<int> ContarUsuariosConectadosAsync(CancellationToken cancelacion = default)
        => (int)await Base.SetLengthAsync(ClavesPresencia.Conectados(_prefijo)).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<IReadOnlyList<PresenciaDto>> LatirYLimpiarAsync(
        TimeSpan margenSinSenal,
        CancellationToken cancelacion = default)
    {
        var ahora = DateTimeOffset.UtcNow;
        var limite = ahora - margenSinSenal;

        var resultado = await Base.ScriptEvaluateAsync(
            GuionesPresencia.LatirYLimpiar,
            values:
            [
                _prefijo,
                _replicaId,
                Marcar(ahora),
                Marcar(limite)
            ]).ConfigureAwait(false);

        var caidos = (RedisValue[]?)resultado ?? [];

        if (caidos.Length == 0)
        {
            return [];
        }

        var desconectados = new List<PresenciaDto>(caidos.Length);

        foreach (var caido in caidos)
        {
            var campos = Separar((string)caido!);

            if (campos.Length < 2)
            {
                continue;
            }

            desconectados.Add(new PresenciaDto(
                Guid.ParseExact(campos[0], "N"),
                campos[1],
                false,
                ahora,
                0));
        }

        _registro.LogWarning(
            "Limpieza de réplicas caídas. UsuariosDesconectados={Total}",
            desconectados.Count);

        return desconectados;
    }

    /// <summary>Compone un valor uniendo sus campos con el separador acordado.</summary>
    /// <param name="campos">Campos en orden.</param>
    private static string Unir(params string[] campos) => string.Join(ClavesPresencia.Separador, campos);

    /// <summary>Descompone un valor en sus campos.</summary>
    /// <param name="valor">Valor almacenado.</param>
    private static string[] Separar(string valor) => valor.Split(ClavesPresencia.Separador);

    /// <summary>
    /// Serializa un instante como número de «ticks» UTC. Es exacto, ordenable y no
    /// depende de la cultura ni del formato con que lo lea el guion Lua.
    /// </summary>
    /// <param name="instante">Instante a serializar.</param>
    private static string Marcar(DateTimeOffset instante)
        => instante.UtcTicks.ToString(CultureInfo.InvariantCulture);

    /// <summary>Recupera un instante serializado con <see cref="Marcar"/>.</summary>
    /// <param name="valor">Valor almacenado.</param>
    private static DateTimeOffset Desmarcar(string valor)
        => new(long.Parse(valor, CultureInfo.InvariantCulture), TimeSpan.Zero);
}
