using Chat.Aplicacion.Abstracciones;
using Chat.Aplicacion.Cqrs;
using Chat.Aplicacion.Dtos;
using Chat.Aplicacion.Mapeos;
using Chat.Aplicacion.Opciones;
using Chat.Aplicacion.Validacion;
using Chat.Dominio.Abstracciones;
using Chat.Dominio.Constantes;
using Chat.Dominio.Entidades;
using Chat.Dominio.Excepciones;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Chat.Aplicacion.Comandos.Usuarios;

/// <summary>Sustituye la foto de perfil del usuario autenticado.</summary>
/// <remarks>
/// La imagen recorre el mismo camino que un adjunto: se descodifica para comprobar que
/// es realmente una imagen, se reescala, se le retiran los metadatos y se vuelve a
/// codificar antes de cifrarla y guardarla. Así, lo que acaba en el almacén nunca es el
/// fichero que envió el cliente, y con él se van la geolocalización y el resto de datos
/// EXIF que el usuario no sabía que estaba publicando.
/// </remarks>
/// <param name="UsuarioId">Usuario que cambia su foto.</param>
/// <param name="Contenido">Flujo con la imagen recibida.</param>
public sealed record ComandoActualizarAvatar(Guid UsuarioId, Stream Contenido) : IComando<PerfilDto>;

/// <summary>Manejador de <see cref="ComandoActualizarAvatar"/>.</summary>
public sealed class ManejadorActualizarAvatar : IManejadorComando<ComandoActualizarAvatar, PerfilDto>
{
    private readonly IRepositorioUsuarios _usuarios;
    private readonly IServicioIdentidad _identidad;
    private readonly IProcesadorImagenes _procesador;
    private readonly ICifradorFlujo _cifrador;
    private readonly IAlmacenObjetos _almacen;
    private readonly IUnidadDeTrabajo _unidadDeTrabajo;
    private readonly IServicioCache _cache;
    private readonly IProveedorFechaHora _reloj;
    private readonly AdjuntosOptions _opciones;
    private readonly ILogger<ManejadorActualizarAvatar> _registro;

    /// <summary>Crea el manejador.</summary>
    public ManejadorActualizarAvatar(
        IRepositorioUsuarios usuarios,
        IServicioIdentidad identidad,
        IProcesadorImagenes procesador,
        ICifradorFlujo cifrador,
        IAlmacenObjetos almacen,
        IUnidadDeTrabajo unidadDeTrabajo,
        IServicioCache cache,
        IProveedorFechaHora reloj,
        IOptions<AdjuntosOptions> opciones,
        ILogger<ManejadorActualizarAvatar> registro)
    {
        _usuarios = usuarios;
        _identidad = identidad;
        _procesador = procesador;
        _cifrador = cifrador;
        _almacen = almacen;
        _unidadDeTrabajo = unidadDeTrabajo;
        _cache = cache;
        _reloj = reloj;
        _opciones = opciones.Value;
        _registro = registro;
    }

    /// <inheritdoc />
    public async Task<PerfilDto> ManejarAsync(
        ComandoActualizarAvatar comando,
        CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(comando);

        if (!_opciones.Activados)
        {
            throw new ExcepcionAutorizacion("La subida de archivos está desactivada en este servidor.");
        }

        var usuarioId = ValidadorEntrada.ValidarIdentificador(comando.UsuarioId, "usuarioId");

        var usuario = await _usuarios.ObtenerPorIdAsync(usuarioId, cancelacion).ConfigureAwait(false)
            ?? throw ExcepcionNoEncontrado.Para("El usuario", usuarioId);

        if (!await _procesador.EsImagenAsync(comando.Contenido, cancelacion).ConfigureAwait(false))
        {
            throw new ExcepcionValidacion("archivo", "La foto de perfil debe ser una imagen.");
        }

        var imagen = await _procesador.NormalizarAsync(comando.Contenido, cancelacion).ConfigureAwait(false);

        // Se escribe en una clave nueva y solo después se retira la anterior: si algo
        // falla a medias, el usuario conserva la foto que ya tenía.
        var claveAnterior = usuario.AvatarClaveObjeto;
        var clave = Usuario.ConstruirClaveAvatar(usuarioId);

        using (var claro = new MemoryStream(imagen.Datos, writable: false))
        {
            await using var cifrado = _cifrador.Cifrar(claro);

            await _almacen.GuardarAsync(
                clave,
                cifrado,
                _cifrador.CalcularTamanoCifrado(imagen.Datos.Length),
                imagen.TipoMime,
                cancelacion).ConfigureAwait(false);
        }

        usuario.AvatarClaveObjeto = clave;
        usuario.AvatarTipoMime = imagen.TipoMime;
        usuario.AvatarActualizado = _reloj.Ahora;

        try
        {
            await _unidadDeTrabajo.GuardarCambiosAsync(cancelacion).ConfigureAwait(false);
        }
        catch
        {
            // La ficha no llegó a guardarse: el objeto recién subido no lo referencia
            // nadie y quedaría ocupando sitio para siempre.
            await RetirarSilenciosamenteAsync(clave).ConfigureAwait(false);
            throw;
        }

        if (!string.IsNullOrEmpty(claveAnterior))
        {
            await RetirarSilenciosamenteAsync(claveAnterior).ConfigureAwait(false);
        }

        // Los listados de usuarios anuncian si hay foto y de cuándo es: hay que
        // rehacerlos para que el resto de clientes se enteren del cambio.
        await _cache.InvalidarPorEtiquetaAsync(ClavesCache.EtiquetaUsuarios, cancelacion).ConfigureAwait(false);

        _registro.LogInformation(
            "Foto de perfil actualizada. UsuarioId={UsuarioId} Mime={Mime} Bytes={Bytes} Clave={Clave}",
            usuarioId,
            imagen.TipoMime,
            imagen.Datos.Length,
            clave);

        var roles = await _identidad.ObtenerRolesAsync(usuario, cancelacion).ConfigureAwait(false);

        return usuario.APerfil(roles.Contains(RolesDelSistema.Administrador, StringComparer.Ordinal));
    }

    /// <summary>Retira un objeto sin dejar que un fallo al hacerlo tape el error original.</summary>
    /// <param name="clave">Clave del objeto a retirar.</param>
    private async Task RetirarSilenciosamenteAsync(string clave)
    {
        try
        {
            await _almacen.EliminarAsync(clave, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception excepcion)
        {
            _registro.LogError(
                excepcion,
                "No se pudo retirar el objeto de una foto de perfil sustituida. Clave={Clave}",
                clave);
        }
    }
}
