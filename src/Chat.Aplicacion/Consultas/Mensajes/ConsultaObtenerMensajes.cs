using Chat.Aplicacion.Abstracciones;
using Chat.Aplicacion.Cqrs;
using Chat.Aplicacion.Dtos;
using Chat.Aplicacion.Mapeos;
using Chat.Aplicacion.Validacion;
using Chat.Dominio.Abstracciones;
using Chat.Dominio.Excepciones;
using Microsoft.Extensions.Logging;

namespace Chat.Aplicacion.Consultas.Mensajes;

/// <summary>Obtiene el historial reciente de una sala, ya descifrado.</summary>
/// <param name="SalaId">Sala consultada.</param>
/// <param name="SolicitanteId">Usuario que consulta; debe ser miembro salvo que sea administrador.</param>
/// <param name="Cantidad">Número máximo de mensajes.</param>
/// <param name="AnteriorA">Devuelve solo mensajes anteriores a esta fecha (paginación hacia atrás).</param>
/// <param name="OmitirComprobacionMembresia">Reservado a administradores.</param>
public sealed record ConsultaObtenerMensajes(
    Guid SalaId,
    Guid SolicitanteId,
    int Cantidad = 50,
    DateTimeOffset? AnteriorA = null,
    bool OmitirComprobacionMembresia = false)
    : IConsulta<IReadOnlyList<MensajeDto>>;

/// <summary>
/// Manejador de <see cref="ConsultaObtenerMensajes"/>.
/// El historial no se cachea: es volátil y contiene datos sensibles ya descifrados.
/// </summary>
public sealed class ManejadorObtenerMensajes
    : IManejadorConsulta<ConsultaObtenerMensajes, IReadOnlyList<MensajeDto>>
{
    /// <summary>Número máximo de mensajes que se pueden pedir en una sola llamada.</summary>
    public const int CantidadMaxima = 200;

    private readonly IRepositorioMensajes _mensajes;
    private readonly IRepositorioSalas _salas;
    private readonly ICifradorMensajes _cifrador;
    private readonly ILogger<ManejadorObtenerMensajes> _registro;

    /// <summary>Crea el manejador.</summary>
    public ManejadorObtenerMensajes(
        IRepositorioMensajes mensajes,
        IRepositorioSalas salas,
        ICifradorMensajes cifrador,
        ILogger<ManejadorObtenerMensajes> registro)
    {
        _mensajes = mensajes;
        _salas = salas;
        _cifrador = cifrador;
        _registro = registro;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<MensajeDto>> ManejarAsync(
        ConsultaObtenerMensajes consulta,
        CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(consulta);

        var salaId = ValidadorEntrada.ValidarIdentificador(consulta.SalaId, "salaId");
        var cantidad = ValidadorEntrada.NormalizarCantidad(consulta.Cantidad, CantidadMaxima);

        var sala = await _salas.ObtenerPorIdAsync(salaId, cancelacion).ConfigureAwait(false)
            ?? throw ExcepcionNoEncontrado.Para("La sala", salaId);

        if (!consulta.OmitirComprobacionMembresia
            && !await _salas.EsMiembroAsync(salaId, consulta.SolicitanteId, cancelacion).ConfigureAwait(false))
        {
            throw new ExcepcionAutorizacion("Solo los miembros de la sala pueden leer su historial.");
        }

        var mensajes = await _mensajes
            .ObtenerRecientesAsync(salaId, cantidad, consulta.AnteriorA, cancelacion)
            .ConfigureAwait(false);

        var resultado = new List<MensajeDto>(mensajes.Count);

        foreach (var mensaje in mensajes)
        {
            // Un mensaje ilegible (clave rotada o dato corrupto) no debe tumbar la consulta:
            // se sustituye por un marcador y se deja constancia en el registro.
            if (!_cifrador.IntentarDescifrar(mensaje.TextoCifrado, out var texto) || texto is null)
            {
                _registro.LogError(
                    "No se pudo descifrar un mensaje. MensajeId={MensajeId} SalaId={SalaId}",
                    mensaje.Id,
                    mensaje.SalaId);

                texto = "[mensaje ilegible: la clave de cifrado no coincide]";
            }

            var nombreAutor = mensaje.Usuario?.UserName ?? Proyecciones.NombreDesconocido;
            resultado.Add(mensaje.ADto(texto, Proyecciones.NombreSalaEnMensaje(sala, nombreAutor), nombreAutor));
        }

        return resultado;
    }
}
