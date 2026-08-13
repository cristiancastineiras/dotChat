using Chat.Aplicacion.Abstracciones;
using Chat.Aplicacion.Cqrs;
using Chat.Aplicacion.Dtos;
using Chat.Aplicacion.Mapeos;
using Chat.Aplicacion.Validacion;
using Chat.Dominio.Abstracciones;
using Chat.Dominio.Constantes;
using Chat.Dominio.Excepciones;

namespace Chat.Aplicacion.Consultas.Usuarios;

/// <summary>Devuelve la identidad del usuario autenticado tal como la ve él mismo.</summary>
/// <remarks>
/// Los datos no se toman del token: un token vive minutos y durante ese tiempo el
/// usuario puede haber cambiado su foto o un administrador haber tocado su cuenta. Se
/// releen de la base de datos para que la interfaz muestre siempre el estado real.
/// </remarks>
/// <param name="UsuarioId">Usuario del que se pide el perfil.</param>
public sealed record ConsultaPerfil(Guid UsuarioId) : IConsulta<PerfilDto>;

/// <summary>Manejador de <see cref="ConsultaPerfil"/>.</summary>
public sealed class ManejadorPerfil : IManejadorConsulta<ConsultaPerfil, PerfilDto>
{
    private readonly IRepositorioUsuarios _usuarios;
    private readonly IServicioIdentidad _identidad;

    /// <summary>Crea el manejador.</summary>
    public ManejadorPerfil(IRepositorioUsuarios usuarios, IServicioIdentidad identidad)
    {
        _usuarios = usuarios;
        _identidad = identidad;
    }

    /// <inheritdoc />
    public async Task<PerfilDto> ManejarAsync(ConsultaPerfil consulta, CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(consulta);

        var usuarioId = ValidadorEntrada.ValidarIdentificador(consulta.UsuarioId, "usuarioId");

        var usuario = await _usuarios.ObtenerPorIdAsync(usuarioId, cancelacion).ConfigureAwait(false)
            ?? throw ExcepcionNoEncontrado.Para("El usuario", usuarioId);

        var roles = await _identidad.ObtenerRolesAsync(usuario, cancelacion).ConfigureAwait(false);

        return usuario.APerfil(roles.Contains(RolesDelSistema.Administrador, StringComparer.Ordinal));
    }
}
