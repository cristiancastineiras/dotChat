using Chat.Aplicacion.Abstracciones;
using Chat.Aplicacion.Cqrs;
using Chat.Aplicacion.Dtos;
using Chat.Aplicacion.Validacion;
using Chat.Dominio.Abstracciones;
using Chat.Dominio.Excepciones;
using Microsoft.Extensions.Logging;

namespace Chat.Aplicacion.Comandos.Autenticacion;

/// <summary>Autentica a un usuario con nombre y contraseña.</summary>
/// <param name="Solicitud">Credenciales presentadas.</param>
public sealed record ComandoIniciarSesion(SolicitudLoginDto Solicitud)
    : IComando<RespuestaAutenticacionDto>;

/// <summary>Manejador de <see cref="ComandoIniciarSesion"/>.</summary>
public sealed class ManejadorIniciarSesion
    : IManejadorComando<ComandoIniciarSesion, RespuestaAutenticacionDto>
{
    private const string MensajeCredencialesInvalidas = "Credenciales no válidas.";

    private readonly IRepositorioUsuarios _usuarios;
    private readonly IServicioIdentidad _identidad;
    private readonly IEmisorSesiones _emisorSesiones;
    private readonly ILogger<ManejadorIniciarSesion> _registro;

    /// <summary>Crea el manejador.</summary>
    public ManejadorIniciarSesion(
        IRepositorioUsuarios usuarios,
        IServicioIdentidad identidad,
        IEmisorSesiones emisorSesiones,
        ILogger<ManejadorIniciarSesion> registro)
    {
        _usuarios = usuarios;
        _identidad = identidad;
        _emisorSesiones = emisorSesiones;
        _registro = registro;
    }

    /// <inheritdoc />
    public async Task<RespuestaAutenticacionDto> ManejarAsync(
        ComandoIniciarSesion comando,
        CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(comando);

        var nombreUsuario = ValidadorEntrada.ValidarNombreUsuario(comando.Solicitud.NombreUsuario);
        var clave = ValidadorEntrada.ValidarClave(comando.Solicitud.Clave);

        var usuario = await _usuarios.ObtenerPorNombreAsync(nombreUsuario, cancelacion).ConfigureAwait(false);

        // El mismo mensaje para «usuario inexistente» y «contraseña incorrecta»:
        // no se revela qué nombres de usuario existen.
        if (usuario is null || !usuario.Activo)
        {
            _registro.LogWarning(
                "Intento de inicio de sesión fallido. NombreUsuario={NombreUsuario} Motivo={Motivo}",
                nombreUsuario,
                usuario is null ? "InexistenteODesconocido" : "CuentaDesactivada");

            throw new ExcepcionAutenticacion(MensajeCredencialesInvalidas);
        }

        if (!await _identidad.VerificarClaveAsync(usuario, clave, cancelacion).ConfigureAwait(false))
        {
            _registro.LogWarning(
                "Intento de inicio de sesión fallido. UsuarioId={UsuarioId} Motivo={Motivo}",
                usuario.Id,
                "ClaveIncorrectaOBloqueo");

            throw new ExcepcionAutenticacion(MensajeCredencialesInvalidas);
        }

        var sesion = await _emisorSesiones.EmitirAsync(usuario, cancelacion).ConfigureAwait(false);

        _registro.LogInformation(
            "Inicio de sesión correcto. UsuarioId={UsuarioId} NombreUsuario={NombreUsuario} Roles={Roles}",
            usuario.Id,
            usuario.UserName,
            string.Join(",", sesion.Roles));

        return sesion;
    }
}
