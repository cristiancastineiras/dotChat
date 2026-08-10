using Chat.Aplicacion.Abstracciones;
using Chat.Aplicacion.Cqrs;
using Chat.Aplicacion.Dtos;
using Chat.Aplicacion.Validacion;
using Chat.Dominio.Abstracciones;
using Chat.Dominio.Excepciones;
using Microsoft.Extensions.Logging;

namespace Chat.Aplicacion.Comandos.Usuarios;

/// <summary>Elimina definitivamente una cuenta. Operación administrativa.</summary>
/// <param name="UsuarioId">Usuario a eliminar.</param>
/// <param name="SolicitanteId">Administrador que ejecuta la operación.</param>
public sealed record ComandoEliminarUsuario(Guid UsuarioId, Guid SolicitanteId)
    : IComando<ResultadoOperacionDto>;

/// <summary>Manejador de <see cref="ComandoEliminarUsuario"/>.</summary>
public sealed class ManejadorEliminarUsuario : IManejadorComando<ComandoEliminarUsuario, ResultadoOperacionDto>
{
    private readonly IRepositorioUsuarios _usuarios;
    private readonly IServicioIdentidad _identidad;
    private readonly IServicioCache _cache;
    private readonly ILogger<ManejadorEliminarUsuario> _registro;

    /// <summary>Crea el manejador.</summary>
    public ManejadorEliminarUsuario(
        IRepositorioUsuarios usuarios,
        IServicioIdentidad identidad,
        IServicioCache cache,
        ILogger<ManejadorEliminarUsuario> registro)
    {
        _usuarios = usuarios;
        _identidad = identidad;
        _cache = cache;
        _registro = registro;
    }

    /// <inheritdoc />
    public async Task<ResultadoOperacionDto> ManejarAsync(
        ComandoEliminarUsuario comando,
        CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(comando);

        var usuarioId = ValidadorEntrada.ValidarIdentificador(comando.UsuarioId, "usuarioId");

        if (usuarioId == comando.SolicitanteId)
        {
            throw new ExcepcionConflicto("Un administrador no puede eliminar su propia cuenta.");
        }

        var usuario = await _usuarios.ObtenerPorIdAsync(usuarioId, cancelacion).ConfigureAwait(false)
            ?? throw ExcepcionNoEncontrado.Para("El usuario", usuarioId);

        var nombre = usuario.UserName ?? usuarioId.ToString();

        var resultado = await _identidad.EliminarUsuarioAsync(usuario, cancelacion).ConfigureAwait(false);
        if (!resultado.Exito)
        {
            throw new ExcepcionConflicto(string.Join(" ", resultado.Errores));
        }

        await _cache.InvalidarPorEtiquetaAsync(ClavesCache.EtiquetaUsuarios, cancelacion).ConfigureAwait(false);
        await _cache.InvalidarPorEtiquetaAsync(ClavesCache.EtiquetaSalas, cancelacion).ConfigureAwait(false);

        _registro.LogWarning(
            "Usuario eliminado. UsuarioId={UsuarioId} NombreUsuario={NombreUsuario} SolicitanteId={SolicitanteId}",
            usuarioId,
            nombre,
            comando.SolicitanteId);

        return new ResultadoOperacionDto(true, $"Usuario '{nombre}' eliminado.");
    }
}
