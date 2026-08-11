using Chat.Aplicacion.Abstracciones;
using Chat.Aplicacion.Cqrs;
using Chat.Aplicacion.Dtos;
using Chat.Aplicacion.Mapeos;
using Chat.Aplicacion.Opciones;
using Chat.Dominio.Abstracciones;
using Microsoft.Extensions.Options;
using ZLinq;

namespace Chat.Aplicacion.Consultas.Usuarios;

/// <summary>Lista los usuarios registrados.</summary>
/// <param name="IncluirInactivos">Incluye también las cuentas desactivadas.</param>
public sealed record ConsultaListarUsuarios(bool IncluirInactivos = false)
    : IConsulta<IReadOnlyList<UsuarioDto>>;

/// <summary>
/// Manejador de <see cref="ConsultaListarUsuarios"/>. El resultado se cachea porque
/// es una consulta frecuente y de baja volatilidad; se invalida al crear o eliminar cuentas.
/// </summary>
public sealed class ManejadorListarUsuarios
    : IManejadorConsulta<ConsultaListarUsuarios, IReadOnlyList<UsuarioDto>>
{
    private readonly IRepositorioUsuarios _usuarios;
    private readonly IServicioCache _cache;
    private readonly IRegistroConexiones _conexiones;
    private readonly CacheOptions _opciones;

    /// <summary>Crea el manejador.</summary>
    public ManejadorListarUsuarios(
        IRepositorioUsuarios usuarios,
        IServicioCache cache,
        IRegistroConexiones conexiones,
        IOptions<CacheOptions> opciones)
    {
        _usuarios = usuarios;
        _cache = cache;
        _conexiones = conexiones;
        _opciones = opciones.Value;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<UsuarioDto>> ManejarAsync(
        ConsultaListarUsuarios consulta,
        CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(consulta);

        var usuarios = await _cache.ObtenerOCrearAsync<IReadOnlyList<UsuarioDto>>(
            ClavesCache.ListaUsuarios(consulta.IncluirInactivos),
            async ct =>
            {
                var registrados = await _usuarios.ListarAsync(consulta.IncluirInactivos, ct).ConfigureAwait(false);
                return registrados.AsValueEnumerable().Select(u => u.ADto()).ToArray();
            },
            TimeSpan.FromSeconds(_opciones.SegundosDuracionUsuarios),
            cancelacion).ConfigureAwait(false);

        // La presencia se resuelve fuera de la caché: es volátil y no debe quedar
        // congelada durante la vigencia de la entrada cacheada. Se pide en bloque para
        // no hacer una consulta por usuario.
        var conectados = await _conexiones
            .FiltrarConectadosAsync([.. usuarios.Select(usuario => usuario.Id)], cancelacion)
            .ConfigureAwait(false);

        return usuarios
            .AsValueEnumerable()
            .Select(usuario => usuario with { EnLinea = conectados.Contains(usuario.Id) })
            .ToArray();
    }
}
