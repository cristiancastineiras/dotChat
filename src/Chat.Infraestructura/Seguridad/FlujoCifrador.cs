using System.Buffers;
using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Chat.Infraestructura.Seguridad;

/// <summary>
/// Flujo de solo lectura que va produciendo el criptograma del contenido de otro flujo
/// a medida que se le pide, sin materializar el fichero completo en memoria.
/// </summary>
/// <remarks>
/// Lee el origen en trozos de <see cref="FormatoFlujoCifrado.TamanoMarco"/> y emite la
/// cabecera y los marcos sellados descritos en <see cref="FormatoFlujoCifrado"/>.
/// Necesita leer un byte por delante para saber si el trozo que tiene entre manos es
/// el último, porque esa condición va autenticada dentro del propio marco.
/// </remarks>
internal sealed class FlujoCifrador : Stream
{
    private readonly Stream _origen;
    private readonly AesGcm _aes;
    private readonly byte[] _contexto;
    private readonly byte[] _semilla = new byte[FormatoFlujoCifrado.TamanoSemilla];

    /// <summary>Búfer de salida: cabecera o marco listo para entregar.</summary>
    private readonly byte[] _salida;

    /// <summary>Búfer de entrada: el trozo en claro que se está sellando.</summary>
    private readonly byte[] _entrada;

    private int _inicioSalida;
    private int _finSalida;
    private int _numeroMarco;
    private bool _origenAgotado;
    private bool _ultimoMarcoEmitido;
    private bool _liberado;

    /// <summary>Bytes ya leídos del origen que aún no se han sellado.</summary>
    private int _pendientesEnEntrada;

    /// <summary>Crea el flujo cifrador.</summary>
    /// <param name="origen">Flujo en claro, legible.</param>
    /// <param name="aes">Cifrador ya inicializado con la clave.</param>
    /// <param name="contexto">Contexto de la aplicación que se autentica en cada marco.</param>
    public FlujoCifrador(Stream origen, AesGcm aes, byte[] contexto)
    {
        ArgumentNullException.ThrowIfNull(origen);

        _origen = origen;
        _aes = aes;
        _contexto = contexto;

        _entrada = ArrayPool<byte>.Shared.Rent(FormatoFlujoCifrado.TamanoMarco);
        _salida = ArrayPool<byte>.Shared.Rent(
            FormatoFlujoCifrado.TamanoCabecera
            + FormatoFlujoCifrado.TamanoCabeceraMarco
            + FormatoFlujoCifrado.TamanoMarco);

        RandomNumberGenerator.Fill(_semilla);

        // La cabecera se deja lista antes de la primera lectura: así el consumidor la
        // recibe aunque el contenido esté vacío.
        FormatoFlujoCifrado.EscribirCabecera(_salida, _semilla);
        _finSalida = FormatoFlujoCifrado.TamanoCabecera;
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

        if (_inicioSalida == _finSalida && !await PrepararSiguienteMarcoAsync(cancelacion).ConfigureAwait(false))
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
    /// Sella el siguiente marco y lo deja en el búfer de salida.
    /// </summary>
    /// <param name="cancelacion">Token de cancelación.</param>
    /// <returns><c>false</c> cuando ya no queda nada que emitir.</returns>
    private async ValueTask<bool> PrepararSiguienteMarcoAsync(CancellationToken cancelacion)
    {
        if (_ultimoMarcoEmitido)
        {
            return false;
        }

        // Se rellena el búfer antes de sellar: hay que saber si detrás viene más
        // contenido para poder marcar el último marco como tal.
        await RellenarEntradaAsync(cancelacion).ConfigureAwait(false);

        var esUltimo = _origenAgotado;

        Span<byte> nonce = stackalloc byte[FormatoFlujoCifrado.TamanoNonce];
        FormatoFlujoCifrado.ComponerNonce(nonce, _semilla, _numeroMarco);

        Span<byte> datosAsociados = stackalloc byte[FormatoFlujoCifrado.TamanoDatosAsociados(_contexto.Length)];
        FormatoFlujoCifrado.ComponerDatosAsociados(datosAsociados, _contexto, _semilla, _numeroMarco, esUltimo);

        var longitud = _pendientesEnEntrada;
        BinaryPrimitives.WriteInt32BigEndian(_salida, longitud);

        var etiqueta = _salida.AsSpan(4, FormatoFlujoCifrado.TamanoEtiqueta);
        var destino = _salida.AsSpan(FormatoFlujoCifrado.TamanoCabeceraMarco, longitud);

        _aes.Encrypt(nonce, _entrada.AsSpan(0, longitud), destino, etiqueta, datosAsociados);

        _inicioSalida = 0;
        _finSalida = FormatoFlujoCifrado.TamanoCabeceraMarco + longitud;
        _pendientesEnEntrada = 0;
        _numeroMarco++;
        _ultimoMarcoEmitido = esUltimo;

        return true;
    }

    /// <summary>
    /// Lee del origen hasta completar un marco o agotarlo. Un flujo puede devolver
    /// menos bytes de los pedidos sin haber terminado, así que se insiste.
    /// </summary>
    /// <param name="cancelacion">Token de cancelación.</param>
    private async ValueTask RellenarEntradaAsync(CancellationToken cancelacion)
    {
        while (_pendientesEnEntrada < FormatoFlujoCifrado.TamanoMarco && !_origenAgotado)
        {
            var leidos = await _origen
                .ReadAsync(_entrada.AsMemory(_pendientesEnEntrada, FormatoFlujoCifrado.TamanoMarco - _pendientesEnEntrada), cancelacion)
                .ConfigureAwait(false);

            if (leidos == 0)
            {
                _origenAgotado = true;
                return;
            }

            _pendientesEnEntrada += leidos;
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
