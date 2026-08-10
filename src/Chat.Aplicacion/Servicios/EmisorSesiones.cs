using Chat.Aplicacion.Abstracciones;
using Chat.Aplicacion.Dtos;
using Chat.Aplicacion.Opciones;
using Chat.Dominio.Abstracciones;
using Chat.Dominio.Entidades;
using Microsoft.Extensions.Options;

namespace Chat.Aplicacion.Servicios;

/// <summary>
/// Emite sesiones: firma el token de acceso, genera un token de refresco aleatorio
/// y persiste únicamente su hash. El valor en claro se entrega una sola vez al cliente.
/// </summary>
public sealed class EmisorSesiones : IEmisorSesiones
{
    private readonly IGeneradorTokens _generador;
    private readonly IServicioIdentidad _identidad;
    private readonly IRepositorioTokensRefresco _tokens;
    private readonly IUnidadDeTrabajo _unidadDeTrabajo;
    private readonly IProveedorFechaHora _reloj;
    private readonly JwtOptions _opciones;

    /// <summary>Crea el emisor de sesiones.</summary>
    public EmisorSesiones(
        IGeneradorTokens generador,
        IServicioIdentidad identidad,
        IRepositorioTokensRefresco tokens,
        IUnidadDeTrabajo unidadDeTrabajo,
        IProveedorFechaHora reloj,
        IOptions<JwtOptions> opciones)
    {
        _generador = generador;
        _identidad = identidad;
        _tokens = tokens;
        _unidadDeTrabajo = unidadDeTrabajo;
        _reloj = reloj;
        _opciones = opciones.Value;
    }

    /// <inheritdoc />
    public async Task<RespuestaAutenticacionDto> EmitirAsync(Usuario usuario, CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(usuario);

        var roles = await _identidad.ObtenerRolesAsync(usuario, cancelacion).ConfigureAwait(false);
        var acceso = _generador.GenerarTokenAcceso(usuario, roles);

        var refrescoEnClaro = _generador.GenerarTokenRefresco();
        var ahora = _reloj.Ahora;

        await _tokens.AgregarAsync(
            new TokenRefresco
            {
                Id = Guid.CreateVersion7(),
                UsuarioId = usuario.Id,
                HashToken = _generador.CalcularHashRefresco(refrescoEnClaro),
                FechaCreacion = ahora,
                FechaExpiracion = ahora.AddDays(_opciones.DiasVigenciaRefresco)
            },
            cancelacion).ConfigureAwait(false);

        usuario.FechaUltimoAcceso = ahora;

        await _unidadDeTrabajo.GuardarCambiosAsync(cancelacion).ConfigureAwait(false);

        return new RespuestaAutenticacionDto(
            usuario.Id,
            usuario.UserName ?? string.Empty,
            acceso.Valor,
            acceso.ExpiraEn,
            refrescoEnClaro,
            roles);
    }
}
