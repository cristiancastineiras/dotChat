using Chat.Aplicacion.Dtos;
using Chat.Dominio.Entidades;

namespace Chat.Aplicacion.Abstracciones;

/// <summary>
/// Emite una sesión completa (token de acceso + token de refresco persistido)
/// para un usuario ya autenticado. Concentra en un único punto la lógica compartida
/// por el registro, el inicio de sesión y la renovación.
/// </summary>
public interface IEmisorSesiones
{
    /// <summary>Genera y persiste una sesión nueva para el usuario indicado.</summary>
    /// <param name="usuario">Usuario autenticado.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    Task<RespuestaAutenticacionDto> EmitirAsync(Usuario usuario, CancellationToken cancelacion = default);
}
