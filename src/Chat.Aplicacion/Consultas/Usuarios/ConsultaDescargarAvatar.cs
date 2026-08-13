using Chat.Aplicacion.Abstracciones;
using Chat.Aplicacion.Cqrs;
using Chat.Aplicacion.Dtos;
using Chat.Aplicacion.Validacion;
using Chat.Dominio.Abstracciones;
using Chat.Dominio.Excepciones;

namespace Chat.Aplicacion.Consultas.Usuarios;

/// <summary>Devuelve la foto de perfil de un usuario, ya descifrada.</summary>
/// <remarks>
/// La foto la puede pedir cualquier usuario autenticado, no solo su dueño: hace falta
/// para dibujar la lista de conversaciones y el autor de cada mensaje. No hay nada
/// privado en ella que no lo sea ya en el nombre de usuario que acompaña.
/// </remarks>
/// <param name="UsuarioId">Usuario cuya foto se pide.</param>
public sealed record ConsultaDescargarAvatar(Guid UsuarioId) : IConsulta<ContenidoAdjuntoDto>;

/// <summary>Manejador de <see cref="ConsultaDescargarAvatar"/>.</summary>
public sealed class ManejadorDescargarAvatar : IManejadorConsulta<ConsultaDescargarAvatar, ContenidoAdjuntoDto>
{
    /// <summary>Tipo con el que se anuncia una foto cuyo MIME no quedó registrado.</summary>
    private const string MimePorDefecto = "image/jpeg";

    private readonly IRepositorioUsuarios _usuarios;
    private readonly ICifradorFlujo _cifrador;
    private readonly IAlmacenObjetos _almacen;

    /// <summary>Crea el manejador.</summary>
    public ManejadorDescargarAvatar(
        IRepositorioUsuarios usuarios,
        ICifradorFlujo cifrador,
        IAlmacenObjetos almacen)
    {
        _usuarios = usuarios;
        _cifrador = cifrador;
        _almacen = almacen;
    }

    /// <inheritdoc />
    public async Task<ContenidoAdjuntoDto> ManejarAsync(
        ConsultaDescargarAvatar consulta,
        CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(consulta);

        var usuarioId = ValidadorEntrada.ValidarIdentificador(consulta.UsuarioId, "usuarioId");

        var usuario = await _usuarios.ObtenerPorIdAsync(usuarioId, cancelacion).ConfigureAwait(false)
            ?? throw ExcepcionNoEncontrado.Para("El usuario", usuarioId);

        if (string.IsNullOrEmpty(usuario.AvatarClaveObjeto))
        {
            throw new ExcepcionNoEncontrado("El usuario no tiene foto de perfil.");
        }

        var cifrado = await _almacen.AbrirAsync(usuario.AvatarClaveObjeto, cancelacion).ConfigureAwait(false);
        var claro = _cifrador.Descifrar(cifrado);

        // El tamaño en claro no se conoce sin leer el flujo entero, y leerlo aquí
        // anularía la ventaja de servirlo en streaming: se deja sin declarar y la
        // respuesta va con codificación por trozos.
        return new ContenidoAdjuntoDto(claro, usuario.AvatarTipoMime ?? MimePorDefecto, "avatar", TamanoBytes: -1);
    }
}
