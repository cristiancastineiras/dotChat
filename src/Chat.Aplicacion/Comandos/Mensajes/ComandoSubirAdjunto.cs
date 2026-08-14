using System.Security.Cryptography;
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

/// <summary>
/// Sube un archivo a una sala y lo deja listo para publicarse en un mensaje.
/// </summary>
/// <remarks>
/// La subida se separa del envío a propósito. Un archivo no cabe en una invocación de
/// SignalR —el hub limita el tamaño de mensaje a decenas de kilobytes— y, sobre todo,
/// separarlas permite validarlo, cifrarlo y almacenarlo sin bloquear la conversación, y
/// reintentar el envío sin volver a subirlo.
/// </remarks>
/// <param name="UsuarioId">Quien sube el archivo.</param>
/// <param name="SalaId">Sala para la que se sube.</param>
/// <param name="NombreArchivo">Nombre original del fichero.</param>
/// <param name="Contenido">Flujo con el contenido recibido.</param>
/// <param name="TamanoDeclarado">Tamaño anunciado por el cliente, en bytes.</param>
/// <param name="DuracionMsDeclarada">
/// Duración en milisegundos, si quien sube declara que es una nota de voz. Solo se usa
/// cuando el contenido resulta ser audio; en cualquier otro caso se ignora.
/// </param>
public sealed record ComandoSubirAdjunto(
    Guid UsuarioId,
    Guid SalaId,
    string NombreArchivo,
    Stream Contenido,
    long TamanoDeclarado,
    long? DuracionMsDeclarada = null) : IComando<AdjuntoDto>;

/// <summary>Manejador de <see cref="ComandoSubirAdjunto"/>.</summary>
public sealed class ManejadorSubirAdjunto : IManejadorComando<ComandoSubirAdjunto, AdjuntoDto>
{
    /// <summary>Duración máxima de una nota de voz que se acepta como declarada, en milisegundos.</summary>
    private const long DuracionMaximaMs = 30 * 60 * 1000;

    private readonly IRepositorioAdjuntos _adjuntos;
    private readonly IRepositorioSalas _salas;
    private readonly IProcesadorImagenes _procesador;
    private readonly IProcesadorAudio _procesadorAudio;
    private readonly ICifradorFlujo _cifrador;
    private readonly IAlmacenObjetos _almacen;
    private readonly IUnidadDeTrabajo _unidadDeTrabajo;
    private readonly IProveedorFechaHora _reloj;
    private readonly AdjuntosOptions _opciones;
    private readonly ILogger<ManejadorSubirAdjunto> _registro;

    /// <summary>Crea el manejador.</summary>
    public ManejadorSubirAdjunto(
        IRepositorioAdjuntos adjuntos,
        IRepositorioSalas salas,
        IProcesadorImagenes procesador,
        IProcesadorAudio procesadorAudio,
        ICifradorFlujo cifrador,
        IAlmacenObjetos almacen,
        IUnidadDeTrabajo unidadDeTrabajo,
        IProveedorFechaHora reloj,
        IOptions<AdjuntosOptions> opciones,
        ILogger<ManejadorSubirAdjunto> registro)
    {
        _adjuntos = adjuntos;
        _salas = salas;
        _procesador = procesador;
        _procesadorAudio = procesadorAudio;
        _cifrador = cifrador;
        _almacen = almacen;
        _unidadDeTrabajo = unidadDeTrabajo;
        _reloj = reloj;
        _opciones = opciones.Value;
        _registro = registro;
    }

    /// <inheritdoc />
    public async Task<AdjuntoDto> ManejarAsync(ComandoSubirAdjunto comando, CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(comando);

        if (!_opciones.Activados)
        {
            throw new ExcepcionAutorizacion("El envío de archivos está desactivado en este servidor.");
        }

        var usuarioId = ValidadorEntrada.ValidarIdentificador(comando.UsuarioId, "usuarioId");
        var salaId = ValidadorEntrada.ValidarIdentificador(comando.SalaId, "salaId");

        _ = await _salas.ObtenerPorIdAsync(salaId, cancelacion).ConfigureAwait(false)
            ?? throw ExcepcionNoEncontrado.Para("La sala", salaId);

        // Se comprueba la pertenencia antes de gastar tiempo en el contenido: si el
        // usuario no está en la sala, no hay nada que procesar.
        if (!await _salas.EsMiembroAsync(salaId, usuarioId, cancelacion).ConfigureAwait(false))
        {
            throw new ExcepcionAutorizacion("Debes unirte a la sala antes de compartir archivos en ella.");
        }

        var nombre = ValidadorEntrada.ValidarNombreArchivo(comando.NombreArchivo);
        var esImagen = await _procesador.EsImagenAsync(comando.Contenido, cancelacion).ConfigureAwait(false);

        // El audio no se recodifica —a diferencia de la imagen, se guarda tal cual—,
        // así que solo hace falta reconocerlo cuando no es ya una imagen.
        var esAudio = !esImagen
            && await _procesadorAudio.EsAudioAsync(comando.Contenido, cancelacion).ConfigureAwait(false);

        var adjuntoId = Guid.CreateVersion7();
        var ahora = _reloj.Ahora;
        var clave = Adjunto.ConstruirClave(salaId, adjuntoId, ahora);

        var almacenado = esImagen
            ? await AlmacenarImagenAsync(clave, comando.Contenido, nombre, cancelacion).ConfigureAwait(false)
            : await AlmacenarArchivoAsync(clave, comando.Contenido, nombre, comando.TamanoDeclarado, cancelacion)
                .ConfigureAwait(false);

        var adjunto = new Adjunto
        {
            Id = adjuntoId,
            SalaId = salaId,
            UsuarioId = usuarioId,
            NombreArchivo = almacenado.Nombre,
            TipoMime = almacenado.TipoMime,
            Tipo = esImagen ? TipoAdjunto.Imagen : esAudio ? TipoAdjunto.Audio : TipoAdjunto.Archivo,
            ClaveObjeto = clave,
            Ancho = almacenado.Ancho,
            Alto = almacenado.Alto,
            DuracionMs = esAudio ? SanearDuracion(comando.DuracionMsDeclarada) : null,
            TamanoBytes = almacenado.Tamano,
            Huella = almacenado.Huella,
            FechaCreacion = ahora
        };

        try
        {
            await _adjuntos.AgregarAsync(adjunto, cancelacion).ConfigureAwait(false);
            await _unidadDeTrabajo.GuardarCambiosAsync(cancelacion).ConfigureAwait(false);
        }
        catch
        {
            // El objeto ya está subido; si la ficha no llega a guardarse, nadie sabría
            // que existe y ocuparía sitio para siempre. Se retira aquí mismo.
            await EliminarSilenciosamenteAsync(clave).ConfigureAwait(false);
            throw;
        }

        _registro.LogInformation(
            "Adjunto subido. AdjuntoId={AdjuntoId} SalaId={SalaId} UsuarioId={UsuarioId} " +
            "Tipo={Tipo} Mime={Mime} Bytes={Bytes} Clave={Clave} Huella={Huella}",
            adjunto.Id,
            salaId,
            usuarioId,
            adjunto.Tipo,
            adjunto.TipoMime,
            adjunto.TamanoBytes,
            clave,
            adjunto.Huella);

        return adjunto.ADto();
    }

    /// <summary>
    /// Normaliza una imagen y la almacena. La imagen sí se materializa en memoria: hay
    /// que descodificarla entera para reescalarla, y por eso su límite de tamaño es más
    /// estrecho que el de un archivo cualquiera.
    /// </summary>
    /// <param name="clave">Clave del objeto en el almacén.</param>
    /// <param name="origen">Contenido recibido.</param>
    /// <param name="nombre">Nombre saneado del fichero.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    private async Task<ContenidoAlmacenado> AlmacenarImagenAsync(
        string clave,
        Stream origen,
        string nombre,
        CancellationToken cancelacion)
    {
        var imagen = await _procesador.NormalizarAsync(origen, cancelacion).ConfigureAwait(false);
        var huella = Convert.ToHexStringLower(SHA256.HashData(imagen.Datos));

        using var claro = new MemoryStream(imagen.Datos, writable: false);
        await SubirCifradoAsync(clave, claro, imagen.Datos.Length, imagen.TipoMime, cancelacion)
            .ConfigureAwait(false);

        return new ContenidoAlmacenado(
            CambiarExtension(nombre, imagen.Extension),
            imagen.TipoMime,
            imagen.Datos.Length,
            huella,
            imagen.Ancho,
            imagen.Alto);
    }

    /// <summary>
    /// Almacena un archivo cualquiera pasándolo en flujo: se lee, se resume, se cifra y
    /// se sube sin cargarlo entero en memoria en ningún momento.
    /// </summary>
    /// <param name="clave">Clave del objeto en el almacén.</param>
    /// <param name="origen">Contenido recibido.</param>
    /// <param name="nombre">Nombre saneado del fichero.</param>
    /// <param name="tamanoDeclarado">Tamaño anunciado por el cliente.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    private async Task<ContenidoAlmacenado> AlmacenarArchivoAsync(
        string clave,
        Stream origen,
        string nombre,
        long tamanoDeclarado,
        CancellationToken cancelacion)
    {
        var tamano = origen.CanSeek ? origen.Length - origen.Position : tamanoDeclarado;

        if (tamano <= 0)
        {
            throw new ExcepcionValidacion("archivo", "El fichero está vacío.");
        }

        if (tamano > _opciones.TamanoMaximoBytes)
        {
            throw new ExcepcionValidacion(
                "archivo",
                $"El archivo no puede superar {_opciones.TamanoMaximoBytes / 1024 / 1024} MiB.");
        }

        using var resumen = SHA256.Create();

        // El resumen se calcula sobre el contenido en claro según pasa hacia el
        // cifrador: una sola lectura sirve para las dos cosas.
        using var conResumen = new CryptoStream(origen, resumen, CryptoStreamMode.Read, leaveOpen: true);

        await SubirCifradoAsync(clave, conResumen, tamano, TiposMime.DeducirDe(nombre), cancelacion)
            .ConfigureAwait(false);

        var huella = Convert.ToHexStringLower(resumen.Hash ?? []);

        return new ContenidoAlmacenado(nombre, TiposMime.DeducirDe(nombre), tamano, huella, null, null);
    }

    /// <summary>Cifra un contenido en claro y lo sube al almacén de objetos.</summary>
    /// <param name="clave">Clave del objeto.</param>
    /// <param name="claro">Contenido sin cifrar.</param>
    /// <param name="tamanoEnClaro">Tamaño del contenido sin cifrar.</param>
    /// <param name="tipoMime">Tipo con el que se anota el objeto.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    private async Task SubirCifradoAsync(
        string clave,
        Stream claro,
        long tamanoEnClaro,
        string tipoMime,
        CancellationToken cancelacion)
    {
        // El almacén necesita saber de antemano cuántos bytes va a recibir, y el flujo
        // cifrado no puede decírselo porque se genera sobre la marcha: el tamaño se
        // calcula a partir del formato, que es determinista.
        var tamanoCifrado = _cifrador.CalcularTamanoCifrado(tamanoEnClaro);

        await using var cifrado = _cifrador.Cifrar(claro);

        await _almacen
            .GuardarAsync(clave, cifrado, tamanoCifrado, tipoMime, cancelacion)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Acota la duración declarada por el cliente a un rango razonable. No es un dato
    /// verificado —el servidor no descodifica el audio para comprobarlo—, así que lo
    /// único que se puede hacer es descartar lo que sea absurdo.
    /// </summary>
    /// <param name="duracionMsDeclarada">Duración declarada por el cliente, si la hay.</param>
    private static int? SanearDuracion(long? duracionMsDeclarada)
    {
        if (duracionMsDeclarada is not { } duracion || duracion <= 0 || duracion > DuracionMaximaMs)
        {
            return null;
        }

        return (int)duracion;
    }

    /// <summary>Retira un objeto sin dejar que un fallo al hacerlo tape el error original.</summary>
    /// <param name="clave">Clave del objeto a retirar.</param>
    private async Task EliminarSilenciosamenteAsync(string clave)
    {
        try
        {
            await _almacen.EliminarAsync(clave, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception excepcion)
        {
            _registro.LogError(
                excepcion,
                "No se pudo retirar el objeto de un adjunto que no llegó a registrarse. Clave={Clave}",
                clave);
        }
    }

    /// <summary>
    /// Sustituye la extensión del nombre por la del formato realmente almacenado, que
    /// no tiene por qué ser el que traía el fichero.
    /// </summary>
    /// <param name="nombre">Nombre ya saneado.</param>
    /// <param name="extension">Extensión del formato de salida, con punto.</param>
    private static string CambiarExtension(string nombre, string extension)
    {
        var punto = nombre.LastIndexOf('.');
        return (punto > 0 ? nombre[..punto] : nombre) + extension;
    }

    /// <summary>Resultado de almacenar un contenido.</summary>
    /// <param name="Nombre">Nombre definitivo del fichero.</param>
    /// <param name="TipoMime">Tipo con el que se sirve.</param>
    /// <param name="Tamano">Tamaño en claro, en bytes.</param>
    /// <param name="Huella">Huella SHA-256 en hexadecimal.</param>
    /// <param name="Ancho">Anchura en píxeles, si es una imagen.</param>
    /// <param name="Alto">Altura en píxeles, si es una imagen.</param>
    private sealed record ContenidoAlmacenado(
        string Nombre,
        string TipoMime,
        long Tamano,
        string Huella,
        int? Ancho,
        int? Alto);
}
