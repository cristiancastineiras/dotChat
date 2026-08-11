using System.Buffers;
using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Chat.Infraestructura.Seguridad;

/// <summary>
/// Flujo de solo lectura que va descifrando marco a marco el criptograma producido por
/// <see cref="FlujoCifrador"/>.
/// </summary>
/// <remarks>
/// Verifica la etiqueta de cada marco antes de entregar un solo byte de ese marco, de
/// modo que un contenido manipulado se detecta durante la lectura y no después. El
/// flujo solo termina correctamente al encontrar un marco autenticado como último: si
/// la transferencia se corta a la mitad, la lectura falla en lugar de devolver un
/// fichero incompleto que parezca bueno.
/// </remarks>
internal sealed class FlujoDescifrador : Stream
{
    private readonly Stream _origen;
    private readonly AesGcm _aes;
    private readonly byte[] _contexto;
    private readonly byte[] _semilla = new byte[FormatoFlujoCifrado.TamanoSemilla];
    private readonly byte[] _entrada;
    private readonly byte[] _salida;

    private int _inicioSalida;
    private int _finSalida;
    private int _numeroMarco;
    private bool _cabeceraLeida;
    private bool _ultimoMarcoLeido;
    private bool _liberado;

    /// <summary>Crea el flujo descifrador.</summary>
    /// <param name="origen">Flujo con el criptograma, legible.</param>
    /// <param name="aes">Cifrador ya inicializado con la clave.</param>
    /// <param name="contexto">Contexto de la aplicación que se autentica en cada marco.</param>
    public FlujoDescifrador(Stream origen, AesGcm aes, byte[] contexto)
    {
        ArgumentNullException.ThrowIfNull(origen);

        _origen = origen;
        _aes = aes;
        _contexto = contexto;

        _entrada = ArrayPool<byte>.Shared.Rent(
            FormatoFlujoCifrado.TamanoCabeceraMarco + FormatoFlujoCifrado.TamanoMarco);
        _salida = ArrayPool<byte>.Shared.Rent(FormatoFlujoCifrado.TamanoMarco);
    }

    /// <inheritdoc />
    public override bool CanRead => true;

    /// <inheritdoc />
    public override bool CanSeek => false;

    /// <inheritdoc />
    public override bool CanWrite => false;

    /// <inheritdoc />
    public override long Length => throw new NotSupportedException();

    /// <inheritdoc />
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    /// <inheritdoc />
    public override async ValueTask<int> ReadAsync(
        Memory<byte> destino,
        CancellationToken cancelacion = default)
    {
        ObjectDisposedException.ThrowIf(_liberado, this);

        if (destino.IsEmpty)
        {
            return 0;
        }

        if (_inicioSalida == _finSalida && !await DescifrarSiguienteMarcoAsync(cancelacion).ConfigureAwait(false))
        {
            return 0;
        }

        var entregados = Math.Min(destino.Length, _finSalida - _inicioSalida);
        _salida.AsMemory(_inicioSalida, entregados).CopyTo(destino);
        _inicioSalida += entregados;

        return entregados;
    }

    /// <inheritdoc />
    public override int Read(byte[] bufer, int desplazamiento, int cantidad)
        => ReadAsync(bufer.AsMemory(desplazamiento, cantidad)).AsTask().GetAwaiter().GetResult();

    /// <inheritdoc />
    public override void Flush()
    {
    }

    /// <inheritdoc />
    public override long Seek(long desplazamiento, SeekOrigin origen) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void SetLength(long valor) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void Write(byte[] bufer, int desplazamiento, int cantidad)
        => throw new NotSupportedException();

    /// <summary>
    /// Lee y verifica el siguiente marco, dejando su contenido en claro en el búfer
    /// de salida.
    /// </summary>
    /// <param name="cancelacion">Token de cancelación.</param>
    /// <returns><c>false</c> cuando el flujo ya se ha consumido entero.</returns>
    /// <exception cref="CryptographicException">Si el criptograma no es íntegro.</exception>
    private async ValueTask<bool> DescifrarSiguienteMarcoAsync(CancellationToken cancelacion)
    {
        if (_ultimoMarcoLeido)
        {
            return false;
        }

        if (!_cabeceraLeida)
        {
            await LeerCabeceraAsync(cancelacion).ConfigureAwait(false);
        }

        await LeerExactamenteAsync(_entrada.AsMemory(0, FormatoFlujoCifrado.TamanoCabeceraMarco), cancelacion)
            .ConfigureAwait(false);

        var longitud = BinaryPrimitives.ReadInt32BigEndian(_entrada);

        if (longitud is < 0 or > FormatoFlujoCifrado.TamanoMarco)
        {
            throw new CryptographicException(
                $"El criptograma declara un marco de {longitud} bytes, fuera del formato esperado.");
        }

        await LeerExactamenteAsync(
            _entrada.AsMemory(FormatoFlujoCifrado.TamanoCabeceraMarco, longitud),
            cancelacion).ConfigureAwait(false);

        Span<byte> nonce = stackalloc byte[FormatoFlujoCifrado.TamanoNonce];
        FormatoFlujoCifrado.ComponerNonce(nonce, _semilla, _numeroMarco);

        var etiqueta = _entrada.AsSpan(4, FormatoFlujoCifrado.TamanoEtiqueta);
        var cifrado = _entrada.AsSpan(FormatoFlujoCifrado.TamanoCabeceraMarco, longitud);
        var claro = _salida.AsSpan(0, longitud);

        // No se sabe de antemano si este marco es el último: se prueba primero como
        // intermedio y, si la etiqueta no cuadra, como último. Solo una de las dos
        // combinaciones puede verificar, porque esa condición va autenticada.
        if (!IntentarAbrir(nonce, cifrado, etiqueta, claro, esUltimo: false))
        {
            if (!IntentarAbrir(nonce, cifrado, etiqueta, claro, esUltimo: true))
            {
                throw new CryptographicException(
                    $"El marco {_numeroMarco} del criptograma no supera la verificación de integridad.");
            }

            _ultimoMarcoLeido = true;
        }

        _inicioSalida = 0;
        _finSalida = longitud;
        _numeroMarco++;

        if (longitud > 0)
        {
            return true;
        }

        // Un marco vacío es legítimo: lo produce un fichero cuyo tamaño es múltiplo
        // exacto del marco, y también un fichero vacío. No hay nada que entregar de él,
        // así que se pasa al siguiente si es que queda alguno.
        return !_ultimoMarcoLeido && await DescifrarSiguienteMarcoAsync(cancelacion).ConfigureAwait(false);
    }

    /// <summary>Intenta verificar y descifrar un marco con la marca de último indicada.</summary>
    /// <param name="nonce">Nonce del marco.</param>
    /// <param name="cifrado">Texto cifrado.</param>
    /// <param name="etiqueta">Etiqueta de autenticación.</param>
    /// <param name="claro">Destino del contenido descifrado.</param>
    /// <param name="esUltimo">Marca de último que se está probando.</param>
    private bool IntentarAbrir(
        ReadOnlySpan<byte> nonce,
        ReadOnlySpan<byte> cifrado,
        ReadOnlySpan<byte> etiqueta,
        Span<byte> claro,
        bool esUltimo)
    {
        Span<byte> datosAsociados = stackalloc byte[FormatoFlujoCifrado.TamanoDatosAsociados(_contexto.Length)];
        FormatoFlujoCifrado.ComponerDatosAsociados(datosAsociados, _contexto, _semilla, _numeroMarco, esUltimo);

        try
        {
            _aes.Decrypt(nonce, cifrado, etiqueta, claro, datosAsociados);
            return true;
        }
        catch (CryptographicException)
        {
            return false;
        }
    }

    /// <summary>Lee y valida la cabecera del flujo.</summary>
    /// <param name="cancelacion">Token de cancelación.</param>
    /// <exception cref="CryptographicException">Si la cabecera no corresponde al formato.</exception>
    private async ValueTask LeerCabeceraAsync(CancellationToken cancelacion)
    {
        await LeerExactamenteAsync(_entrada.AsMemory(0, FormatoFlujoCifrado.TamanoCabecera), cancelacion)
            .ConfigureAwait(false);

        if (!_entrada.AsSpan(0, 4).SequenceEqual(FormatoFlujoCifrado.Magia))
        {
            throw new CryptographicException("El contenido no tiene el formato de flujo cifrado de dotChat.");
        }

        if (_entrada[4] != FormatoFlujoCifrado.Version)
        {
            throw new CryptographicException($"Versión de flujo cifrado no soportada: {_entrada[4]}.");
        }

        var tamanoMarco = BinaryPrimitives.ReadInt32BigEndian(_entrada.AsSpan(5));

        if (tamanoMarco != FormatoFlujoCifrado.TamanoMarco)
        {
            throw new CryptographicException(
                $"El criptograma usa marcos de {tamanoMarco} bytes y esta versión solo lee de {FormatoFlujoCifrado.TamanoMarco}.");
        }

        _entrada.AsSpan(9, FormatoFlujoCifrado.TamanoSemilla).CopyTo(_semilla);
        _cabeceraLeida = true;
    }

    /// <summary>
    /// Lee exactamente los bytes pedidos. Que un flujo devuelva menos de lo solicitado
    /// es normal; que se agote a mitad de un marco significa que el criptograma está
    /// truncado.
    /// </summary>
    /// <param name="destino">Búfer a rellenar por completo.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    /// <exception cref="CryptographicException">Si el origen se agota antes de tiempo.</exception>
    private async ValueTask LeerExactamenteAsync(Memory<byte> destino, CancellationToken cancelacion)
    {
        var leidosTotal = 0;

        while (leidosTotal < destino.Length)
        {
            var leidos = await _origen.ReadAsync(destino[leidosTotal..], cancelacion).ConfigureAwait(false);

            if (leidos == 0)
            {
                throw new CryptographicException(
                    "El criptograma está truncado: la transferencia terminó a mitad de un marco.");
            }

            leidosTotal += leidos;
        }
    }

    /// <inheritdoc />
    protected override void Dispose(bool liberando)
    {
        if (!_liberado && liberando)
        {
            ArrayPool<byte>.Shared.Return(_entrada, clearArray: true);
            ArrayPool<byte>.Shared.Return(_salida, clearArray: true);
            CryptographicOperations.ZeroMemory(_semilla);
            _origen.Dispose();
            _liberado = true;
        }

        base.Dispose(liberando);
    }

    /// <inheritdoc />
    public override async ValueTask DisposeAsync()
    {
        if (!_liberado)
        {
            ArrayPool<byte>.Shared.Return(_entrada, clearArray: true);
            ArrayPool<byte>.Shared.Return(_salida, clearArray: true);
            CryptographicOperations.ZeroMemory(_semilla);
            await _origen.DisposeAsync().ConfigureAwait(false);
            _liberado = true;
        }

        await base.DisposeAsync().ConfigureAwait(false);
    }
}
