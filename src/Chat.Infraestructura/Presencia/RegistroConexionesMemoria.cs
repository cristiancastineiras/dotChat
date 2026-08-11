using Chat.Aplicacion.Abstracciones;
using Chat.Aplicacion.Dtos;
using ZLinq;

namespace Chat.Infraestructura.Presencia;

/// <summary>
/// Registro de conexiones y presencia local al proceso, para cuando Valkey está
/// desactivado y el servidor corre en una sola instancia.
/// </summary>
/// <remarks>
/// <para>
/// Es el modo mínimo: arrancar el servidor sin más dependencias que la base de datos.
/// En cuanto se levanta una segunda réplica deja de servir, porque cada una tendría su
/// propia idea de quién está conectado; por eso el modo distribuido es el de por
/// defecto y este solo se activa desactivando Valkey a propósito.
/// </para>
/// <para>
/// Se protege con un cerrojo único en lugar de con diccionarios concurrentes porque hay
/// que mantener coherentes dos vistas del mismo estado: un contador de conexiones por
/// usuario solo es fiable si se actualiza a la vez que la conexión que lo provoca. Las
/// secciones críticas son de unos pocos nanosegundos.
/// </para>
/// </remarks>
public sealed class RegistroConexionesMemoria : IRegistroConexiones
{
    private readonly Lock _cerrojo = new();
    private readonly Dictionary<string, Conexion> _conexiones = new(StringComparer.Ordinal);
    private readonly Dictionary<Guid, Presencia> _presencias = [];

    /// <inheritdoc />
    public Task<bool> RegistrarAsync(
        string conexionId,
        Guid usuarioId,
        string nombreUsuario,
        DateTimeOffset fechaConexion,
        CancellationToken cancelacion = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(conexionId);

        lock (_cerrojo)
        {
            _conexiones[conexionId] = new Conexion(usuarioId, nombreUsuario, fechaConexion);

            var abiertas = _presencias.TryGetValue(usuarioId, out var actual) ? actual.Conexiones + 1 : 1;
            _presencias[usuarioId] = new Presencia(nombreUsuario, abiertas, fechaConexion);

            return Task.FromResult(abiertas == 1);
        }
    }

    /// <inheritdoc />
    public Task<ConexionCerrada?> EliminarAsync(
        string conexionId,
        DateTimeOffset fechaDesconexion,
        CancellationToken cancelacion = default)
    {
        lock (_cerrojo)
        {
            if (!_conexiones.Remove(conexionId, out var conexion))
            {
                return Task.FromResult<ConexionCerrada?>(null);
            }

            var abiertas = 0;

            if (_presencias.TryGetValue(conexion.UsuarioId, out var actual))
            {
                abiertas = Math.Max(0, actual.Conexiones - 1);

                // El registro del usuario se conserva aunque ya no tenga conexiones:
                // es lo que permite responder «visto por última vez a las…».
                _presencias[conexion.UsuarioId] = actual with
                {
                    Conexiones = abiertas,
                    UltimaVez = fechaDesconexion
                };
            }

            return Task.FromResult<ConexionCerrada?>(
                new ConexionCerrada(conexion.UsuarioId, conexion.NombreUsuario, abiertas == 0));
        }
    }

    /// <inheritdoc />
    public Task AgregarSalaAsync(string conexionId, string nombreSala, CancellationToken cancelacion = default)
    {
        lock (_cerrojo)
        {
            if (_conexiones.TryGetValue(conexionId, out var conexion))
            {
                conexion.Salas.Add(nombreSala);
            }
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task QuitarSalaAsync(string conexionId, string nombreSala, CancellationToken cancelacion = default)
    {
        lock (_cerrojo)
        {
            if (_conexiones.TryGetValue(conexionId, out var conexion))
            {
                conexion.Salas.Remove(nombreSala);
            }
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<ConexionActivaDto>> ListarAsync(CancellationToken cancelacion = default)
    {
        lock (_cerrojo)
        {
            // ZLinq proyecta sobre estructuras: la única asignación es el array final.
            IReadOnlyList<ConexionActivaDto> conexiones = _conexiones
                .AsValueEnumerable()
                .Select(par => new ConexionActivaDto(
                    par.Key,
                    par.Value.UsuarioId,
                    par.Value.NombreUsuario,
                    par.Value.FechaConexion,
                    [.. par.Value.Salas.AsValueEnumerable().Order(StringComparer.OrdinalIgnoreCase)]))
                .OrderBy(conexion => conexion.FechaConexion)
                .ToArray();

            return Task.FromResult(conexiones);
        }
    }

    /// <inheritdoc />
    public Task<bool> EstaConectadoAsync(Guid usuarioId, CancellationToken cancelacion = default)
    {
        lock (_cerrojo)
        {
            return Task.FromResult(
                _presencias.TryGetValue(usuarioId, out var presencia) && presencia.Conexiones > 0);
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlySet<Guid>> FiltrarConectadosAsync(
        IReadOnlyCollection<Guid> usuarioIds,
        CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(usuarioIds);

        lock (_cerrojo)
        {
            var conectados = new HashSet<Guid>();

            foreach (var usuarioId in usuarioIds)
            {
                if (_presencias.TryGetValue(usuarioId, out var presencia) && presencia.Conexiones > 0)
                {
                    conectados.Add(usuarioId);
                }
            }

            return Task.FromResult<IReadOnlySet<Guid>>(conectados);
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> ConexionesDeAsync(
        Guid usuarioId,
        CancellationToken cancelacion = default)
    {
        lock (_cerrojo)
        {
            IReadOnlyList<string> conexiones = _conexiones
                .AsValueEnumerable()
                .Where(par => par.Value.UsuarioId == usuarioId)
                .Select(par => par.Key)
                .ToArray();

            return Task.FromResult(conexiones);
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<PresenciaDto>> ListarPresenciaAsync(CancellationToken cancelacion = default)
    {
        lock (_cerrojo)
        {
            IReadOnlyList<PresenciaDto> presencias = _presencias
                .AsValueEnumerable()
                .Select(par => new PresenciaDto(
                    par.Key,
                    par.Value.Nombre,
                    par.Value.Conexiones > 0,
                    par.Value.UltimaVez,
                    par.Value.Conexiones))
                .OrderByDescending(presencia => presencia.EnLinea)
                .ThenBy(presencia => presencia.NombreUsuario, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            return Task.FromResult(presencias);
        }
    }

    /// <inheritdoc />
    public Task<int> ContarConexionesAsync(CancellationToken cancelacion = default)
    {
        lock (_cerrojo)
        {
            return Task.FromResult(_conexiones.Count);
        }
    }

    /// <inheritdoc />
    public Task<int> ContarUsuariosConectadosAsync(CancellationToken cancelacion = default)
    {
        lock (_cerrojo)
        {
            var total = 0;

            foreach (var presencia in _presencias.Values)
            {
                if (presencia.Conexiones > 0)
                {
                    total++;
                }
            }

            return Task.FromResult(total);
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// No hay nada que limpiar: si este proceso cae, se lleva consigo todo el estado y
    /// no queda ninguna conexión fantasma que retirar.
    /// </remarks>
    public Task<IReadOnlyList<PresenciaDto>> LatirYLimpiarAsync(
        TimeSpan margenSinSenal,
        CancellationToken cancelacion = default)
        => Task.FromResult<IReadOnlyList<PresenciaDto>>([]);

    /// <summary>Datos de una conexión activa.</summary>
    /// <param name="UsuarioId">Usuario propietario.</param>
    /// <param name="NombreUsuario">Nombre del usuario.</param>
    /// <param name="FechaConexion">Instante de conexión.</param>
    private sealed record Conexion(Guid UsuarioId, string NombreUsuario, DateTimeOffset FechaConexion)
    {
        /// <summary>Salas a las que está suscrita la conexión.</summary>
        public HashSet<string> Salas { get; } = new(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Presencia acumulada de un usuario.</summary>
    /// <param name="Nombre">Último nombre conocido.</param>
    /// <param name="Conexiones">Conexiones abiertas en este momento.</param>
    /// <param name="UltimaVez">Instante de la última conexión o desconexión observada.</param>
    private sealed record Presencia(string Nombre, int Conexiones, DateTimeOffset UltimaVez);
}
