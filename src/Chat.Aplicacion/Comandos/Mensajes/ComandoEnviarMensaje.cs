using Chat.Aplicacion.Abstracciones;
using Chat.Aplicacion.Cqrs;
using Chat.Aplicacion.Dtos;
using Chat.Aplicacion.Mapeos;
using Chat.Aplicacion.Mensajeria;
using Chat.Aplicacion.Opciones;
using Chat.Aplicacion.Validacion;
using Chat.Dominio.Abstracciones;
using Chat.Dominio.Entidades;
using Chat.Dominio.Excepciones;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Chat.Aplicacion.Comandos.Mensajes;

/// <summary>Publica un mensaje en una sala, cifrándolo antes de persistirlo.</summary>
/// <param name="UsuarioId">Autor del mensaje.</param>
/// <param name="Solicitud">Contenido, adjunto y sala destino.</param>
public sealed record ComandoEnviarMensaje(Guid UsuarioId, SolicitudEnviarMensajeDto Solicitud)
    : IComando<MensajeDto>;

/// <summary>Manejador de <see cref="ComandoEnviarMensaje"/>.</summary>
public sealed class ManejadorEnviarMensaje : IManejadorComando<ComandoEnviarMensaje, MensajeDto>
{
    private readonly IRepositorioMensajes _mensajes;
    private readonly IRepositorioSalas _salas;
    private readonly IRepositorioUsuarios _usuarios;
    private readonly IRepositorioAdjuntos _adjuntos;
    private readonly ICifradorMensajes _cifrador;
    private readonly IProtectorRepeticion _protectorRepeticion;
    private readonly INotificadorTiempoReal _notificador;
    private readonly IUnidadDeTrabajo _unidadDeTrabajo;
    private readonly IProveedorFechaHora _reloj;
    private readonly CifradoOptions _opcionesCifrado;
    private readonly ILogger<ManejadorEnviarMensaje> _registro;

    /// <summary>Crea el manejador.</summary>
    public ManejadorEnviarMensaje(
        IRepositorioMensajes mensajes,
        IRepositorioSalas salas,
        IRepositorioUsuarios usuarios,
        IRepositorioAdjuntos adjuntos,
        ICifradorMensajes cifrador,
        IProtectorRepeticion protectorRepeticion,
        INotificadorTiempoReal notificador,
        IUnidadDeTrabajo unidadDeTrabajo,
        IProveedorFechaHora reloj,
        IOptions<CifradoOptions> opcionesCifrado,
        ILogger<ManejadorEnviarMensaje> registro)
    {
        _mensajes = mensajes;
        _salas = salas;
        _usuarios = usuarios;
        _adjuntos = adjuntos;
        _cifrador = cifrador;
        _protectorRepeticion = protectorRepeticion;
        _notificador = notificador;
        _unidadDeTrabajo = unidadDeTrabajo;
        _reloj = reloj;
        _opcionesCifrado = opcionesCifrado.Value;
        _registro = registro;
    }

    /// <inheritdoc />
    public async Task<MensajeDto> ManejarAsync(ComandoEnviarMensaje comando, CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(comando);

        var usuarioId = ValidadorEntrada.ValidarIdentificador(comando.UsuarioId, "usuarioId");
        var salaId = ValidadorEntrada.ValidarIdentificador(comando.Solicitud.SalaId, "salaId");
        var envioId = ValidadorEntrada.ValidarIdentificador(comando.Solicitud.IdentificadorEnvio, "identificadorEnvio");
        var llevaAdjunto = comando.Solicitud.AdjuntoId is { } identificador && identificador != Guid.Empty;

        // Los atajos se expanden antes de medir la longitud: lo que cuenta para el
        // límite es el emoji resultante, no el «:sonrisa:» que el usuario tecleó.
        var textoExpandido = CatalogoEmojis.Expandir(comando.Solicitud.Texto);

        // Sin imagen, el texto es obligatorio; con ella, es un pie de foto opcional.
        var texto = llevaAdjunto
            ? ValidadorEntrada.ValidarPieDeFoto(textoExpandido, _opcionesCifrado.LongitudMaximaMensaje)
            : ValidadorEntrada.ValidarTextoMensaje(textoExpandido, _opcionesCifrado.LongitudMaximaMensaje);

        // Protección básica contra repetición: un mismo identificador de envío
        // solo se acepta una vez dentro de la ventana configurada.
        var esNuevo = await _protectorRepeticion
            .RegistrarSiEsNuevoAsync("mensaje", envioId.ToString(), cancelacion)
            .ConfigureAwait(false);

        if (!esNuevo)
        {
            throw new ExcepcionConflicto("El mensaje ya se había enviado (identificador de envío repetido).");
        }

        var sala = await _salas.ObtenerPorIdAsync(salaId, cancelacion).ConfigureAwait(false)
            ?? throw ExcepcionNoEncontrado.Para("La sala", salaId);

        if (!await _salas.EsMiembroAsync(salaId, usuarioId, cancelacion).ConfigureAwait(false))
        {
            throw new ExcepcionAutorizacion("Debes unirte a la sala antes de escribir en ella.");
        }

        var usuario = await _usuarios.ObtenerPorIdAsync(usuarioId, cancelacion).ConfigureAwait(false)
            ?? throw ExcepcionNoEncontrado.Para("El usuario", usuarioId);

        var adjunto = llevaAdjunto
            ? await ResolverAdjuntoAsync(comando.Solicitud.AdjuntoId!.Value, usuarioId, salaId, cancelacion)
                .ConfigureAwait(false)
            : null;

        var ahora = _reloj.Ahora;

        var mensaje = new Mensaje
        {
            Id = Guid.CreateVersion7(),
            SalaId = salaId,
            UsuarioId = usuarioId,
            TextoCifrado = texto is null ? null : _cifrador.Cifrar(texto),
            AdjuntoId = adjunto?.Id,
            FechaEnvio = ahora
        };

        // La sala llega seguida por el contexto: basta con tocar la marca de actividad
        // para que la bandeja de los clientes ordene por conversación más reciente.
        // No se invalida el catálogo cacheado: haría inútil la caché a cada mensaje y
        // la bandeja del usuario, que sí es exacta, se sirve siempre sin cachear.
        sala.FechaUltimaActividad = ahora;

        await _mensajes.AgregarAsync(mensaje, cancelacion).ConfigureAwait(false);
        await _unidadDeTrabajo.GuardarCambiosAsync(cancelacion).ConfigureAwait(false);

        // La navegación se asigna después de guardar para que la proyección incluya
        // los metadatos del adjunto sin volver a consultarlos.
        mensaje.Adjunto = adjunto;

        var nombreAutor = usuario.UserName ?? Proyecciones.NombreDesconocido;
        var dto = mensaje.ADto(texto ?? string.Empty, Proyecciones.NombreSalaEnMensaje(sala, nombreAutor), nombreAutor);
        await _notificador.NotificarMensajeAsync(dto, cancelacion).ConfigureAwait(false);

        // Se registra el hecho y su tamaño, nunca el contenido del mensaje.
        _registro.LogInformation(
            "Mensaje enviado. MensajeId={MensajeId} SalaId={SalaId} UsuarioId={UsuarioId} Longitud={Longitud} AdjuntoId={AdjuntoId}",
            mensaje.Id,
            salaId,
            usuarioId,
            texto?.Length ?? 0,
            adjunto?.Id);

        return dto;
    }

    /// <summary>
    /// Recupera el adjunto y comprueba que quien lo publica es quien lo subió, que
    /// va a la misma sala para la que se subió y que no se ha usado ya.
    /// </summary>
    /// <remarks>
    /// Sin estas tres comprobaciones, conocer un identificador bastaría para colar la
    /// imagen de otra persona en otra conversación.
    /// </remarks>
    /// <param name="adjuntoId">Adjunto solicitado.</param>
    /// <param name="usuarioId">Autor del mensaje.</param>
    /// <param name="salaId">Sala destino.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    private async Task<Adjunto> ResolverAdjuntoAsync(
        Guid adjuntoId,
        Guid usuarioId,
        Guid salaId,
        CancellationToken cancelacion)
    {
        var adjunto = await _adjuntos.ObtenerPorIdAsync(adjuntoId, cancelacion).ConfigureAwait(false)
            ?? throw ExcepcionNoEncontrado.Para("El adjunto", adjuntoId);

        if (adjunto.UsuarioId != usuarioId || adjunto.SalaId != salaId)
        {
            throw new ExcepcionAutorizacion("El adjunto no pertenece a esta conversación.");
        }

        if (await _adjuntos.EstaPublicadoAsync(adjuntoId, cancelacion).ConfigureAwait(false))
        {
            throw new ExcepcionConflicto("Esa imagen ya se publicó en otro mensaje.");
        }

        return adjunto;
    }
}
