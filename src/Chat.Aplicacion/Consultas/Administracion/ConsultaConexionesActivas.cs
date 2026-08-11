using Chat.Aplicacion.Abstracciones;
using Chat.Aplicacion.Cqrs;
using Chat.Aplicacion.Dtos;

namespace Chat.Aplicacion.Consultas.Administracion;

/// <summary>Lista las conexiones SignalR abiertas en este momento.</summary>
public sealed record ConsultaConexionesActivas : IConsulta<IReadOnlyList<ConexionActivaDto>>;

/// <summary>Manejador de <see cref="ConsultaConexionesActivas"/>.</summary>
public sealed class ManejadorConexionesActivas
    : IManejadorConsulta<ConsultaConexionesActivas, IReadOnlyList<ConexionActivaDto>>
{
    private readonly IRegistroConexiones _conexiones;

    /// <summary>Crea el manejador.</summary>
    public ManejadorConexionesActivas(IRegistroConexiones conexiones) => _conexiones = conexiones;

    /// <inheritdoc />
    public Task<IReadOnlyList<ConexionActivaDto>> ManejarAsync(
        ConsultaConexionesActivas consulta,
        CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(consulta);
        return _conexiones.ListarAsync(cancelacion);
    }
}
