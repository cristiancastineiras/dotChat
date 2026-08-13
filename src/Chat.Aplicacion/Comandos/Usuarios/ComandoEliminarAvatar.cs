using Chat.Aplicacion.Abstracciones;
using Chat.Aplicacion.Cqrs;
using Chat.Aplicacion.Dtos;
using Chat.Aplicacion.Mapeos;
using Chat.Aplicacion.Validacion;
using Chat.Dominio.Abstracciones;
using Chat.Dominio.Constantes;
using Chat.Dominio.Excepciones;
using Microsoft.Extensions.Logging;

namespace Chat.Aplicacion.Comandos.Usuarios;

/// <summary>Retira la foto de perfil del usuario autenticado y vuelve a las iniciales.</summary>
/// <param name="UsuarioId">Usuario que retira su foto.</param>
public sealed record ComandoEliminarAvatar(Guid UsuarioId) : IComando<PerfilDto>;

/// <summary>Manejador de <see cref="ComandoEliminarAvatar"/>.</summary>
public sealed class ManejadorEliminarAvatar : IManejadorComando<ComandoEliminarAvatar, PerfilDto>
{
    private readonly IRepositorioUsuarios _usuarios;
    private readonly IServicioIdentidad _identidad;
    private readonly IAlmacenObjetos _almacen;
    private readonly IUnidadDeTrabajo _unidadDeTrabajo;
    private readonly IServicioCache _cache;
    private readonly ILogger<ManejadorEliminarAvatar> _registro;

    /// <summary>Crea el manejador.</summary>
    public ManejadorEliminarAvatar(
        IRepositorioUsuarios usuarios,
        IServicioIdentidad identidad,
        IAlmacenObjetos almacen,
        IUnidadDeTrabajo unidadDeTrabajo,
        IServicioCache cache,
        ILogger<ManejadorEliminarAvatar> registro)
    {
        _usuarios = usuarios;
        _identidad = identidad;
        _almacen = almacen;
        _unidadDeTrabajo = unidadDeTrabajo;
        _cache = cache;
        _registro = registro;
    }

    /// <inheritdoc />
    public async Task<PerfilDto> ManejarAsync(
        ComandoEliminarAvatar comando,
        CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(comando);

        var usuarioId = ValidadorEntrada.ValidarIdentificador(comando.UsuarioId, "usuarioId");

        var usuario = await _usuarios.ObtenerPorIdAsync(usuarioId, cancelacion).ConfigureAwait(false)
            ?? throw ExcepcionNoEncontrado.Para("El usuario", usuarioId);

        var clave = usuario.AvatarClaveObjeto;

        if (!string.IsNullOrEmpty(clave))
        {
            usuario.AvatarClaveObjeto = null;
            usuario.AvatarTipoMime = null;
            usuario.AvatarActualizado = null;

            // Primero la ficha y después el objeto: si el borrado del objeto falla, lo
            // peor que queda es un huérfano invisible, no una foto que ya no se puede leer.
            await _unidadDeTrabajo.GuardarCambiosAsync(cancelacion).ConfigureAwait(false);

            try
            {
                await _almacen.EliminarAsync(clave, cancelacion).ConfigureAwait(false);
            }
            catch (Exception excepcion)
            {
                _registro.LogError(
                    excepcion,
                    "No se pudo retirar el objeto de una foto de perfil eliminada. Clave={Clave}",
                    clave);
            }

            await _cache.InvalidarPorEtiquetaAsync(ClavesCache.EtiquetaUsuarios, cancelacion).ConfigureAwait(false);

            _registro.LogInformation("Foto de perfil eliminada. UsuarioId={UsuarioId}", usuarioId);
        }

        var roles = await _identidad.ObtenerRolesAsync(usuario, cancelacion).ConfigureAwait(false);

        return usuario.APerfil(roles.Contains(RolesDelSistema.Administrador, StringComparer.Ordinal));
    }
}
