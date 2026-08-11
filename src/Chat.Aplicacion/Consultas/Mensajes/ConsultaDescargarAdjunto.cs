using Chat.Aplicacion.Abstracciones;
using Chat.Aplicacion.Cqrs;
using Chat.Aplicacion.Dtos;
using Chat.Aplicacion.Validacion;
using Chat.Dominio.Abstracciones;
using Chat.Dominio.Excepciones;

namespace Chat.Aplicacion.Consultas.Mensajes;

/// <summary>Abre el contenido de un archivo adjunto para entregárselo a quien lo pide.</summary>
/// <param name="AdjuntoId">Adjunto solicitado.</param>
/// <param name="SolicitanteId">Quien lo pide; debe ser miembro de la sala.</param>
/// <param name="OmitirComprobacionMembresia">Reservado a administradores.</param>
public sealed record ConsultaDescargarAdjunto(
    Guid AdjuntoId,
    Guid SolicitanteId,
    bool OmitirComprobacionMembresia = false) : IConsulta<ContenidoAdjuntoDto>;

/// <summary>
/// Manejador de <see cref="ConsultaDescargarAdjunto"/>.
/// </summary>
/// <remarks>
/// El contenido no se cachea ni se materializa: se devuelve el flujo descifrado tal
/// cual, para que viaje del almacén de objetos al cliente sin pasar por la memoria del
/// servidor. Quien recibe el resultado es responsable de liberarlo.
/// </remarks>
public sealed class ManejadorDescargarAdjunto
    : IManejadorConsulta<ConsultaDescargarAdjunto, ContenidoAdjuntoDto>
{
    private readonly IRepositorioAdjuntos _adjuntos;
    private readonly IRepositorioSalas _salas;
    private readonly IAlmacenObjetos _almacen;
    private readonly ICifradorFlujo _cifrador;

    /// <summary>Crea el manejador.</summary>
    public ManejadorDescargarAdjunto(
        IRepositorioAdjuntos adjuntos,
        IRepositorioSalas salas,
        IAlmacenObjetos almacen,
        ICifradorFlujo cifrador)
    {
        _adjuntos = adjuntos;
        _salas = salas;
        _almacen = almacen;
        _cifrador = cifrador;
    }

    /// <inheritdoc />
    public async Task<ContenidoAdjuntoDto> ManejarAsync(
        ConsultaDescargarAdjunto consulta,
        CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(consulta);

        var adjuntoId = ValidadorEntrada.ValidarIdentificador(consulta.AdjuntoId, "adjuntoId");

        var adjunto = await _adjuntos.ObtenerPorIdAsync(adjuntoId, cancelacion).ConfigureAwait(false)
            ?? throw ExcepcionNoEncontrado.Para("El adjunto", adjuntoId);

        // La autorización se resuelve por la sala del adjunto, no por quién lo subió:
        // cualquier miembro de la conversación puede ver lo que se comparte en ella.
        if (!consulta.OmitirComprobacionMembresia
            && !await _salas.EsMiembroAsync(adjunto.SalaId, consulta.SolicitanteId, cancelacion).ConfigureAwait(false))
        {
            throw new ExcepcionAutorizacion("Solo los miembros de la sala pueden descargar sus archivos.");
        }

        var cifrado = await _almacen.AbrirAsync(adjunto.ClaveObjeto, cancelacion).ConfigureAwait(false);

        // El descifrador se queda con el flujo del almacén y lo libera con él, así que
        // no hace falta ninguna gestión adicional aquí.
        return new ContenidoAdjuntoDto(
            _cifrador.Descifrar(cifrado),
            adjunto.TipoMime,
            adjunto.NombreArchivo,
            adjunto.TamanoBytes);
    }
}
