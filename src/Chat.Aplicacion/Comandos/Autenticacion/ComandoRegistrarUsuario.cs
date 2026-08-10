using Chat.Aplicacion.Abstracciones;
using Chat.Aplicacion.Cqrs;
using Chat.Aplicacion.Dtos;
using Chat.Aplicacion.Validacion;
using Chat.Dominio.Abstracciones;
using Chat.Dominio.Constantes;
using Chat.Dominio.Entidades;
using Chat.Dominio.Excepciones;
using Microsoft.Extensions.Logging;

namespace Chat.Aplicacion.Comandos.Autenticacion;

/// <summary>Registra una cuenta nueva y devuelve la sesión ya iniciada.</summary>
/// <param name="Solicitud">Datos de alta.</param>
public sealed record ComandoRegistrarUsuario(SolicitudRegistroDto Solicitud)
    : IComando<RespuestaAutenticacionDto>;

/// <summary>Manejador de <see cref="ComandoRegistrarUsuario"/>.</summary>
public sealed class ManejadorRegistrarUsuario
    : IManejadorComando<ComandoRegistrarUsuario, RespuestaAutenticacionDto>
{
    private readonly IRepositorioUsuarios _usuarios;
    private readonly IServicioIdentidad _identidad;
    private readonly IEmisorSesiones _emisorSesiones;
    private readonly IServicioCache _cache;
    private readonly IProveedorFechaHora _reloj;
    private readonly ILogger<ManejadorRegistrarUsuario> _registro;

    /// <summary>Crea el manejador.</summary>
    public ManejadorRegistrarUsuario(
        IRepositorioUsuarios usuarios,
        IServicioIdentidad identidad,
        IEmisorSesiones emisorSesiones,
        IServicioCache cache,
        IProveedorFechaHora reloj,
        ILogger<ManejadorRegistrarUsuario> registro)
    {
        _usuarios = usuarios;
        _identidad = identidad;
        _emisorSesiones = emisorSesiones;
        _cache = cache;
        _reloj = reloj;
        _registro = registro;
    }

    /// <inheritdoc />
    public async Task<RespuestaAutenticacionDto> ManejarAsync(
        ComandoRegistrarUsuario comando,
        CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(comando);

        var nombreUsuario = ValidadorEntrada.ValidarNombreUsuario(comando.Solicitud.NombreUsuario);
        var email = ValidadorEntrada.ValidarEmail(comando.Solicitud.Email);
        var clave = ValidadorEntrada.ValidarClave(comando.Solicitud.Clave);

        if (await _usuarios.ObtenerPorNombreAsync(nombreUsuario, cancelacion).ConfigureAwait(false) is not null)
        {
            throw new ExcepcionConflicto($"Ya existe un usuario con el nombre '{nombreUsuario}'.");
        }

        if (await _identidad.ExisteEmailAsync(email, cancelacion).ConfigureAwait(false))
        {
            throw new ExcepcionConflicto("Ya existe una cuenta asociada a ese correo electrónico.");
        }

        var usuario = new Usuario
        {
            Id = Guid.CreateVersion7(),
            UserName = nombreUsuario,
            Email = email,
            FechaCreacion = _reloj.Ahora,
            Activo = true
        };

        var resultado = await _identidad.CrearUsuarioAsync(usuario, clave, cancelacion).ConfigureAwait(false);
        if (!resultado.Exito)
        {
            throw new ExcepcionValidacion(
                new Dictionary<string, string[]> { ["clave"] = [.. resultado.Errores] });
        }

        var asignacion = await _identidad.AsignarRolAsync(usuario, RolesDelSistema.Usuario, cancelacion).ConfigureAwait(false);
        if (!asignacion.Exito)
        {
            throw new ExcepcionConflicto(string.Join(" ", asignacion.Errores));
        }

        await _cache.InvalidarPorEtiquetaAsync(ClavesCache.EtiquetaUsuarios, cancelacion).ConfigureAwait(false);

        _registro.LogInformation(
            "Usuario registrado. UsuarioId={UsuarioId} NombreUsuario={NombreUsuario}",
            usuario.Id,
            usuario.UserName);

        return await _emisorSesiones.EmitirAsync(usuario, cancelacion).ConfigureAwait(false);
    }
}
