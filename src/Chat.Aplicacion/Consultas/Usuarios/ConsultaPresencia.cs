using Chat.Aplicacion.Abstracciones;
using Chat.Aplicacion.Cqrs;
using Chat.Aplicacion.Dtos;

namespace Chat.Aplicacion.Consultas.Usuarios;

/// <summary>
/// Devuelve el estado de conexión de los usuarios conocidos por el servidor desde
/// que arrancó: quién está en línea ahora y cuándo se vio por última vez al resto.
/// </summary>
public sealed record ConsultaPresencia : IConsulta<IReadOnlyList<PresenciaDto>>;

/// <summary>Manejador de <see cref="ConsultaPresencia"/>.</summary>
public sealed class ManejadorPresencia : IManejadorConsulta<ConsultaPresencia, IReadOnlyList<PresenciaDto>>
{
    private readonly IRegistroConexiones _conexiones;

    /// <summary>Crea el manejador.</summary>
    /// <param name="conexiones">Registro de conexiones en memoria.</param>
    public ManejadorPresencia(IRegistroConexiones conexiones) => _conexiones = conexiones;

    /// <inheritdoc />
    public Task<IReadOnlyList<PresenciaDto>> ManejarAsync(
        ConsultaPresencia consulta,
        CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(consulta);
        return Task.FromResult(_conexiones.ListarPresencia());
    }
}
