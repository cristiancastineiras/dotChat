using Chat.Aplicacion.Abstracciones;
using Chat.Aplicacion.Dtos;
using ZLinq;

namespace Chat.Servidor.Servicios;

/// <summary>
/// Registro en memoria de las conexiones SignalR activas y de la presencia asociada.
/// </summary>
/// <remarks>
/// El estado es intencionadamente local al proceso: la plataforma está pensada
/// para una única instancia local (on-premise). Para escalar horizontalmente
/// habría que sustituir esta implementación por un almacén compartido, sin tocar
/// el resto del código gracias a <see cref="IRegistroConexiones"/>.
/// <para>
/// Se protege con un cerrojo único en lugar de con diccionarios concurrentes porque
/// hay que mantener coherentes dos vistas del mismo estado (conexiones y presencia):
/// un contador de conexiones por usuario solo es fiable si se actualiza a la vez que
/// la conexión que lo provoca. Las secciones críticas son de unos pocos nanosegundos.
/// </para>
/// </remarks>
public sealed class RegistroConexiones : IRegistroConexiones
{
    private readonly Lock _cerrojo = new();
    private readonly Dictionary<string, Conexion> _conexiones = new(StringComparer.Ordinal);
    private readonly Dictionary<Guid, Presencia> _presencias = [];

    /// <inheritdoc />
    public int TotalConexiones
    {
        get
        {
            lock (_cerrojo)
            {
                return _conexiones.Count;
            }
        }
    }

    /// <inheritdoc />
    public int TotalUsuariosConectados
    {
        get
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

                return total;
            }
        }
    }

    /// <inheritdoc />
    public bool Registrar(string conexionId, Guid usuarioId, string nombreUsuario, DateTimeOffset fechaConexion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(conexionId);

        lock (_cerrojo)
        {
            _conexiones[conexionId] = new Conexion(usuarioId, nombreUsuario, fechaConexion);

            var abiertas = _presencias.TryGetValue(usuarioId, out var actual) ? actual.Conexiones + 1 : 1;
            _presencias[usuarioId] = new Presencia(nombreUsuario, abiertas, fechaConexion);

            return abiertas == 1;
        }
    }

    /// <inheritdoc />
    public ConexionCerrada? Eliminar(string conexionId, DateTimeOffset fechaDesconexion)
    {
        lock (_cerrojo)
        {
            if (!_conexiones.Remove(conexionId, out var conexion))
            {
                return null;
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

            return new ConexionCerrada(conexion.UsuarioId, conexion.NombreUsuario, abiertas == 0);
        }
    }

    /// <inheritdoc />
    public void AgregarSala(string conexionId, string nombreSala)
    {
        lock (_cerrojo)
        {
            if (_conexiones.TryGetValue(conexionId, out var conexion))
            {
                conexion.Salas.Add(nombreSala);
            }
        }
    }

    /// <inheritdoc />
    public void QuitarSala(string conexionId, string nombreSala)
    {
        lock (_cerrojo)
        {
            if (_conexiones.TryGetValue(conexionId, out var conexion))
            {
                conexion.Salas.Remove(nombreSala);
            }
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<ConexionActivaDto> Listar()
    {
        lock (_cerrojo)
        {
            // ZLinq proyecta sobre estructuras: la única asignación es el array final.
            return _conexiones
                .AsValueEnumerable()
                .Select(par => new ConexionActivaDto(
                    par.Key,
                    par.Value.UsuarioId,
                    par.Value.NombreUsuario,
                    par.Value.FechaConexion,
                    [.. par.Value.Salas.AsValueEnumerable().Order(StringComparer.OrdinalIgnoreCase)]))
                .OrderBy(conexion => conexion.FechaConexion)
                .ToArray();
        }
    }

    /// <inheritdoc />
    public bool EstaConectado(Guid usuarioId)
    {
        lock (_cerrojo)
        {
            return _presencias.TryGetValue(usuarioId, out var presencia) && presencia.Conexiones > 0;
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<string> ConexionesDe(Guid usuarioId)
    {
        lock (_cerrojo)
        {
            return _conexiones
                .AsValueEnumerable()
                .Where(par => par.Value.UsuarioId == usuarioId)
                .Select(par => par.Key)
                .ToArray();
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<PresenciaDto> ListarPresencia()
    {
        lock (_cerrojo)
        {
            return _presencias
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
        }
    }

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
