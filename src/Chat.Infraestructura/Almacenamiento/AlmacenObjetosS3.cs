using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Chat.Aplicacion.Abstracciones;
using Chat.Aplicacion.Opciones;
using Chat.Dominio.Excepciones;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Chat.Infraestructura.Almacenamiento;

/// <summary>
/// Implementación de <see cref="IAlmacenObjetos"/> sobre el protocolo de S3, que es el
/// que habla MinIO.
/// </summary>
/// <remarks>
/// El cliente de S3 es seguro para uso concurrente y mantiene su propio grupo de
/// conexiones, así que se registra como instancia única y se comparte.
/// </remarks>
public sealed class AlmacenObjetosS3 : IAlmacenObjetos, IDisposable
{
    private readonly IAmazonS3 _cliente;
    private readonly AlmacenObjetosOptions _opciones;
    private readonly ILogger<AlmacenObjetosS3> _registro;
    private bool _liberado;

    /// <summary>Crea el almacén.</summary>
    /// <param name="opciones">Configuración del almacén.</param>
    /// <param name="registro">Registro estructurado.</param>
    public AlmacenObjetosS3(IOptions<AlmacenObjetosOptions> opciones, ILogger<AlmacenObjetosS3> registro)
    {
        ArgumentNullException.ThrowIfNull(opciones);

        _opciones = opciones.Value;
        _registro = registro;

        var configuracion = new AmazonS3Config
        {
            ServiceURL = _opciones.PuntoEntrada,
            ForcePathStyle = _opciones.EstiloRuta,
            AuthenticationRegion = _opciones.Region,
            Timeout = TimeSpan.FromSeconds(_opciones.SegundosTiempoEspera),

            // El SDK reintenta por su cuenta los fallos transitorios de red y las
            // respuestas 5xx, que es exactamente lo que hace falta contra un almacén
            // que puede estar reiniciándose.
            RetryMode = RequestRetryMode.Standard,
            MaxErrorRetry = 3
        };

        _cliente = new AmazonS3Client(
            new BasicAWSCredentials(_opciones.ClaveAcceso, _opciones.ClaveSecreta),
            configuracion);
    }

    /// <inheritdoc />
    public async Task PrepararAsync(CancellationToken cancelacion = default)
    {
        if (await ExisteContenedorAsync(cancelacion).ConfigureAwait(false))
        {
            _registro.LogInformation(
                "Almacén de objetos listo. Contenedor={Contenedor} PuntoEntrada={PuntoEntrada}",
                _opciones.Contenedor,
                _opciones.PuntoEntrada);

            return;
        }

        if (!_opciones.CrearContenedorSiFalta)
        {
            throw new InvalidOperationException(
                $"El contenedor '{_opciones.Contenedor}' no existe y la creación automática está desactivada.");
        }

        await _cliente
            .PutBucketAsync(new PutBucketRequest { BucketName = _opciones.Contenedor }, cancelacion)
            .ConfigureAwait(false);

        _registro.LogInformation("Contenedor de adjuntos creado. Contenedor={Contenedor}", _opciones.Contenedor);
    }

    /// <inheritdoc />
    public async Task GuardarAsync(
        string clave,
        Stream contenido,
        long tamano,
        string tipoMime,
        CancellationToken cancelacion = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clave);
        ArgumentNullException.ThrowIfNull(contenido);
        ObjectDisposedException.ThrowIf(_liberado, this);

        var peticion = new PutObjectRequest
        {
            BucketName = _opciones.Contenedor,
            Key = clave,
            InputStream = contenido,
            ContentType = tipoMime,

            // El flujo cifrado no admite búsqueda ni conoce su longitud por
            // adelantado, así que hay que decirle al SDK cuánto va a recibir. Con
            // envío troceado (aws-chunked) el SDK firma cada fragmento sobre la
            // marcha sin necesitar releer ni buscar en el flujo; es la alternativa a
            // "DisablePayloadSigning", que el propio SDK rechaza salvo que el
            // almacén hable HTTPS, y MinIO se habla en claro dentro de la red interna
            // del compose.
            AutoCloseStream = false,
            UseChunkEncoding = true,
            Headers = { ContentLength = tamano }
        };

        await _cliente.PutObjectAsync(peticion, cancelacion).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<Stream> AbrirAsync(string clave, CancellationToken cancelacion = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clave);
        ObjectDisposedException.ThrowIf(_liberado, this);

        try
        {
            var respuesta = await _cliente
                .GetObjectAsync(_opciones.Contenedor, clave, cancelacion)
                .ConfigureAwait(false);

            return respuesta.ResponseStream;
        }
        catch (AmazonS3Exception excepcion) when (excepcion.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            throw new ExcepcionNoEncontrado(
                "El contenido del adjunto ya no está disponible en el almacén de objetos.");
        }
    }

    /// <inheritdoc />
    public async Task EliminarAsync(string clave, CancellationToken cancelacion = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clave);
        ObjectDisposedException.ThrowIf(_liberado, this);

        try
        {
            await _cliente.DeleteObjectAsync(_opciones.Contenedor, clave, cancelacion).ConfigureAwait(false);
        }
        catch (AmazonS3Exception excepcion) when (excepcion.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // Borrar lo que ya no está es el resultado que se buscaba.
        }
    }

    /// <inheritdoc />
    public async Task EliminarVariosAsync(
        IReadOnlyCollection<string> claves,
        CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(claves);
        ObjectDisposedException.ThrowIf(_liberado, this);

        if (claves.Count == 0)
        {
            return;
        }

        // El protocolo limita cada borrado múltiple a mil objetos.
        foreach (var lote in claves.Chunk(1000))
        {
            var peticion = new DeleteObjectsRequest
            {
                BucketName = _opciones.Contenedor,
                Objects = [.. lote.Select(clave => new KeyVersion { Key = clave })]
            };

            try
            {
                await _cliente.DeleteObjectsAsync(peticion, cancelacion).ConfigureAwait(false);
            }
            catch (DeleteObjectsException excepcion)
            {
                // Un borrado parcial no debe interrumpir la purga: lo que quede se
                // volverá a intentar en la siguiente pasada de mantenimiento.
                _registro.LogWarning(
                    excepcion,
                    "Algunos objetos no se pudieron borrar. Fallidos={Fallidos}",
                    excepcion.Response.DeleteErrors.Count);
            }
        }
    }

    /// <inheritdoc />
    public async Task<bool> RespondeAsync(CancellationToken cancelacion = default)
    {
        try
        {
            return await ExisteContenedorAsync(cancelacion).ConfigureAwait(false);
        }
        catch (Exception excepcion) when (excepcion is not OperationCanceledException)
        {
            return false;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_liberado)
        {
            return;
        }

        _cliente.Dispose();
        _liberado = true;
    }

    /// <summary>Comprueba si el contenedor configurado existe y es accesible.</summary>
    /// <param name="cancelacion">Token de cancelación.</param>
    private async Task<bool> ExisteContenedorAsync(CancellationToken cancelacion)
    {
        try
        {
            // Se pregunta por el contenedor en sí en lugar de listar todos los del
            // servicio: así basta con permisos sobre este y no sobre la cuenta entera.
            await _cliente
                .GetBucketLocationAsync(new GetBucketLocationRequest { BucketName = _opciones.Contenedor }, cancelacion)
                .ConfigureAwait(false);

            return true;
        }
        catch (AmazonS3Exception excepcion) when (
            excepcion.StatusCode is System.Net.HttpStatusCode.NotFound
            || excepcion.ErrorCode is "NoSuchBucket")
        {
            return false;
        }
    }
}
