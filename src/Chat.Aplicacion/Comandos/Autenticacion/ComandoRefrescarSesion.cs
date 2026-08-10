using Chat.Aplicacion.Abstracciones;
using Chat.Aplicacion.Cqrs;
using Chat.Aplicacion.Dtos;
using Chat.Dominio.Abstracciones;
using Chat.Dominio.Excepciones;
using Microsoft.Extensions.Logging;

namespace Chat.Aplicacion.Comandos.Autenticacion;

/// <summary>Renueva la sesión a partir de un token de refresco válido.</summary>
/// <param name="Solicitud">Token de refresco presentado.</param>
public sealed record ComandoRefrescarSesion(SolicitudRefrescoDto Solicitud)
    : IComando<RespuestaAutenticacionDto>;

/// <summary>
/// Manejador de <see cref="ComandoRefrescarSesion"/>. Aplica rotación de tokens:
/// el token usado se revoca inmediatamente y se emite uno nuevo, de forma que la
/// reutilización de un token antiguo (repetición) siempre falla.
/// </summary>
public sealed class ManejadorRefrescarSesion
    : IManejadorComando<ComandoRefrescarSesion, RespuestaAutenticacionDto>
{
    private const string MensajeTokenInvalido = "El token de refresco no es válido o ha caducado.";

    private readonly IRepositorioTokensRefresco _tokens;
    private readonly IRepositorioUsuarios _usuarios;
    private readonly IGeneradorTokens _generador;
    private readonly IEmisorSesiones _emisorSesiones;
    private readonly IUnidadDeTrabajo _unidadDeTrabajo;
    private readonly IProveedorFechaHora _reloj;
    private readonly ILogger<ManejadorRefrescarSesion> _registro;

    /// <summary>Crea el manejador.</summary>
    public ManejadorRefrescarSesion(
        IRepositorioTokensRefresco tokens,
        IRepositorioUsuarios usuarios,
        IGeneradorTokens generador,
        IEmisorSesiones emisorSesiones,
        IUnidadDeTrabajo unidadDeTrabajo,
        IProveedorFechaHora reloj,
        ILogger<ManejadorRefrescarSesion> registro)
    {
        _tokens = tokens;
        _usuarios = usuarios;
        _generador = generador;
        _emisorSesiones = emisorSesiones;
        _unidadDeTrabajo = unidadDeTrabajo;
        _reloj = reloj;
        _registro = registro;
    }

    /// <inheritdoc />
    public async Task<RespuestaAutenticacionDto> ManejarAsync(
        ComandoRefrescarSesion comando,
        CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(comando);

        if (string.IsNullOrWhiteSpace(comando.Solicitud.TokenRefresco))
        {
            throw new ExcepcionValidacion("tokenRefresco", "El token de refresco es obligatorio.");
        }

        var hash = _generador.CalcularHashRefresco(comando.Solicitud.TokenRefresco);
        var almacenado = await _tokens.ObtenerPorHashAsync(hash, cancelacion).ConfigureAwait(false)
            ?? throw new ExcepcionAutenticacion(MensajeTokenInvalido);

        var ahora = _reloj.Ahora;

        if (!almacenado.EsValido(ahora))
        {
            // Presentar un token ya revocado indica robo o repetición: se cierran
            // todas las sesiones del usuario por precaución.
            if (almacenado.EstaRevocado)
            {
                await _tokens.RevocarTodosAsync(almacenado.UsuarioId, ahora, cancelacion).ConfigureAwait(false);
                await _unidadDeTrabajo.GuardarCambiosAsync(cancelacion).ConfigureAwait(false);

                _registro.LogWarning(
                    "Se detectó la reutilización de un token de refresco revocado; se cerraron todas las sesiones. UsuarioId={UsuarioId}",
                    almacenado.UsuarioId);
            }

            throw new ExcepcionAutenticacion(MensajeTokenInvalido);
        }

        var usuario = await _usuarios.ObtenerPorIdAsync(almacenado.UsuarioId, cancelacion).ConfigureAwait(false);
        if (usuario is null || !usuario.Activo)
        {
            throw new ExcepcionAutenticacion(MensajeTokenInvalido);
        }

        almacenado.Revocar(ahora);

        var sesion = await _emisorSesiones.EmitirAsync(usuario, cancelacion).ConfigureAwait(false);

        _registro.LogInformation("Sesión renovada. UsuarioId={UsuarioId}", usuario.Id);

        return sesion;
    }
}
