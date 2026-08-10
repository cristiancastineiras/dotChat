using Chat.Aplicacion.Abstracciones;
using Chat.Aplicacion.Cqrs;
using Chat.Aplicacion.Dtos;
using Chat.Dominio.Abstracciones;

namespace Chat.Aplicacion.Consultas.Administracion;

/// <summary>Resumen de actividad de la plataforma.</summary>
public sealed record ConsultaEstadisticas : IConsulta<EstadisticasDto>;

/// <summary>Manejador de <see cref="ConsultaEstadisticas"/>.</summary>
public sealed class ManejadorEstadisticas : IManejadorConsulta<ConsultaEstadisticas, EstadisticasDto>
{
    private readonly IRepositorioUsuarios _usuarios;
    private readonly IRepositorioSalas _salas;
    private readonly IRepositorioMensajes _mensajes;
    private readonly IRegistroConexiones _conexiones;
    private readonly IProveedorFechaHora _reloj;

    /// <summary>Crea el manejador.</summary>
    public ManejadorEstadisticas(
        IRepositorioUsuarios usuarios,
        IRepositorioSalas salas,
        IRepositorioMensajes mensajes,
        IRegistroConexiones conexiones,
        IProveedorFechaHora reloj)
    {
        _usuarios = usuarios;
        _salas = salas;
        _mensajes = mensajes;
        _conexiones = conexiones;
        _reloj = reloj;
    }

    /// <inheritdoc />
    public async Task<EstadisticasDto> ManejarAsync(
        ConsultaEstadisticas consulta,
        CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(consulta);

        // Las tres cuentas comparten el mismo DbContext, que no admite consultas
        // concurrentes: se ejecutan de forma secuencial de manera deliberada.
        var totalUsuarios = await _usuarios.ContarAsync(cancelacion).ConfigureAwait(false);
        var totalSalas = await _salas.ContarAsync(cancelacion).ConfigureAwait(false);
        var totalMensajes = await _mensajes.ContarAsync(null, cancelacion).ConfigureAwait(false);

        return new EstadisticasDto(
            totalUsuarios,
            totalSalas,
            totalMensajes,
            _conexiones.TotalConexiones,
            _conexiones.TotalUsuariosConectados,
            _reloj.Ahora);
    }
}
