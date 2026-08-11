using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Chat.Aplicacion.Abstracciones;
using Chat.Aplicacion.Opciones;
using Microsoft.Extensions.Options;

namespace Chat.Infraestructura.Seguridad;

/// <summary>
/// Cifrador de mensajes basado en AES-256-GCM (cifrado autenticado).
/// </summary>
/// <remarks>
/// <para>Formato del criptograma, codificado en Base64:</para>
/// <code>
/// [ versión (1 byte) ][ nonce (12 bytes) ][ etiqueta (16 bytes) ][ texto cifrado (n bytes) ]
/// </code>
/// <para>
/// El nonce es aleatorio y distinto en cada operación, de modo que dos mensajes
/// idénticos producen criptogramas diferentes. La etiqueta GCM garantiza que
/// cualquier manipulación del dato almacenado se detecte al descifrar.
/// El «contexto asociado» se incluye como AAD y liga el criptograma a esta aplicación.
/// </para>
/// <para>La clave se toma siempre de la configuración; nunca está en el código.</para>
/// </remarks>
public sealed class ServicioCifradorMensajes : ICifradorMensajes, ICifradorFlujo, IDisposable
{
    /// <summary>Versión del formato de criptograma; permite migrar el esquema en el futuro.</summary>
    private const byte VersionFormato = 1;

    /// <summary>Tamaño del nonce recomendado para AES-GCM.</summary>
    private const int TamanoNonce = 12;

    /// <summary>Tamaño de la etiqueta de autenticación (máximo admitido por GCM).</summary>
    private const int TamanoEtiqueta = 16;

    /// <summary>Tamaño de clave exigido: 256 bits.</summary>
    private const int TamanoClave = 32;

    /// <summary>Sufijo que distingue el contexto asociado del contenido binario.</summary>
    private const string SufijoContextoBinario = ":binario";

    private readonly AesGcm _aes;
    private readonly byte[] _datosAsociados;
    private readonly byte[] _datosAsociadosBinarios;
    private bool _liberado;

    /// <summary>Crea el cifrador a partir de la configuración.</summary>
    /// <param name="opciones">Opciones de cifrado (clave y contexto asociado).</param>
    /// <exception cref="InvalidOperationException">Si la clave falta o no mide 32 bytes.</exception>
    public ServicioCifradorMensajes(IOptions<CifradoOptions> opciones)
    {
        ArgumentNullException.ThrowIfNull(opciones);

        var configuracion = opciones.Value;
        var clave = LeerClave(configuracion.ClaveBase64);

        _aes = new AesGcm(clave, TamanoEtiqueta);
        _datosAsociados = Encoding.UTF8.GetBytes(configuracion.ContextoAsociado);
        _datosAsociadosBinarios = Encoding.UTF8.GetBytes(configuracion.ContextoAsociado + SufijoContextoBinario);

        // La copia local de la clave deja de ser necesaria en cuanto AesGcm la asume.
        CryptographicOperations.ZeroMemory(clave);
    }

    /// <inheritdoc />
    public string Cifrar(string textoPlano)
    {
        ArgumentNullException.ThrowIfNull(textoPlano);

        var bytesPlanos = Encoding.UTF8.GetBytes(textoPlano);

        try
        {
            return Convert.ToBase64String(Sellar(bytesPlanos, _datosAsociados));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytesPlanos);
        }
    }

    /// <inheritdoc />
    public string Descifrar(string textoCifrado)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(textoCifrado);

        byte[] entrada;
        try
        {
            entrada = Convert.FromBase64String(textoCifrado);
        }
        catch (FormatException excepcion)
        {
            throw new CryptographicException("El criptograma no está codificado en Base64 válido.", excepcion);
        }

        var bytesPlanos = Abrir(entrada, _datosAsociados);

        try
        {
            return Encoding.UTF8.GetString(bytesPlanos);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytesPlanos);
        }
    }

    /// <inheritdoc />
    public bool IntentarDescifrar(string textoCifrado, out string? textoPlano)
    {
        try
        {
            textoPlano = Descifrar(textoCifrado);
            return true;
        }
        catch (Exception excepcion) when (excepcion is CryptographicException or ArgumentException)
        {
            textoPlano = null;
            return false;
        }
    }

    /// <inheritdoc />
    public byte[] CifrarBinario(ReadOnlySpan<byte> datosPlanos) => Sellar(datosPlanos, _datosAsociadosBinarios);

    /// <inheritdoc />
    public bool IntentarDescifrarBinario(byte[] datosCifrados, out byte[]? datosPlanos)
    {
        ArgumentNullException.ThrowIfNull(datosCifrados);

        try
        {
            datosPlanos = Abrir(datosCifrados, _datosAsociadosBinarios);
            return true;
        }
        catch (Exception excepcion) when (excepcion is CryptographicException or ArgumentException)
        {
            datosPlanos = null;
            return false;
        }
    }

    /// <inheritdoc />
    public Stream Cifrar(Stream claro)
    {
        ObjectDisposedException.ThrowIf(_liberado, this);
        return new FlujoCifrador(claro, _aes, _datosAsociadosBinarios);
    }

    /// <inheritdoc />
    public Stream Descifrar(Stream cifrado)
    {
        ObjectDisposedException.ThrowIf(_liberado, this);
        return new FlujoDescifrador(cifrado, _aes, _datosAsociadosBinarios);
    }

    /// <inheritdoc />
    public long CalcularTamanoCifrado(long tamanoEnClaro)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(tamanoEnClaro);

        // Siempre hay un marco más de los que salen de la división: el que cierra el
        // flujo, que va vacío cuando el tamaño es múltiplo exacto del marco.
        var marcos = (tamanoEnClaro / FormatoFlujoCifrado.TamanoMarco) + 1;

        return FormatoFlujoCifrado.TamanoCabecera
            + (marcos * FormatoFlujoCifrado.TamanoCabeceraMarco)
            + tamanoEnClaro;
    }

    /// <summary>
    /// Construye el criptograma completo: versión, nonce aleatorio, etiqueta de
    /// autenticación y datos cifrados, en un único búfer.
    /// </summary>
    /// <param name="planos">Contenido original.</param>
    /// <param name="datosAsociados">Contexto que se autentica junto al contenido.</param>
    private byte[] Sellar(ReadOnlySpan<byte> planos, byte[] datosAsociados)
    {
        ObjectDisposedException.ThrowIf(_liberado, this);

        var salida = new byte[1 + TamanoNonce + TamanoEtiqueta + planos.Length];
        salida[0] = VersionFormato;

        var nonce = salida.AsSpan(1, TamanoNonce);
        var etiqueta = salida.AsSpan(1 + TamanoNonce, TamanoEtiqueta);
        var cifrado = salida.AsSpan(1 + TamanoNonce + TamanoEtiqueta);

        RandomNumberGenerator.Fill(nonce);
        _aes.Encrypt(nonce, planos, cifrado, etiqueta, datosAsociados);

        return salida;
    }

    /// <summary>Comprueba el formato de un criptograma, verifica su etiqueta y lo descifra.</summary>
    /// <param name="entrada">Criptograma completo.</param>
    /// <param name="datosAsociados">Contexto con el que se selló.</param>
    /// <exception cref="CryptographicException">
    /// Si el criptograma está truncado, usa otra versión de formato, se manipuló o
    /// se cifró con otra clave o en otro contexto.
    /// </exception>
    private byte[] Abrir(ReadOnlySpan<byte> entrada, byte[] datosAsociados)
    {
        ObjectDisposedException.ThrowIf(_liberado, this);

        if (entrada.Length < 1 + TamanoNonce + TamanoEtiqueta)
        {
            throw new CryptographicException("El criptograma está truncado o no tiene el formato esperado.");
        }

        if (entrada[0] != VersionFormato)
        {
            throw new CryptographicException($"Versión de criptograma no soportada: {entrada[0]}.");
        }

        var nonce = entrada.Slice(1, TamanoNonce);
        var etiqueta = entrada.Slice(1 + TamanoNonce, TamanoEtiqueta);
        var cifrado = entrada[(1 + TamanoNonce + TamanoEtiqueta)..];
        var planos = new byte[cifrado.Length];

        // Lanza CryptographicException si la etiqueta no cuadra: dato manipulado o clave distinta.
        _aes.Decrypt(nonce, cifrado, etiqueta, planos, datosAsociados);

        return planos;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_liberado)
        {
            return;
        }

        _aes.Dispose();
        _liberado = true;
    }

    /// <summary>Decodifica y valida la clave configurada.</summary>
    /// <param name="claveBase64">Clave en Base64.</param>
    /// <returns>Clave binaria de 32 bytes.</returns>
    private static byte[] LeerClave(string claveBase64)
    {
        if (string.IsNullOrWhiteSpace(claveBase64))
        {
            throw new InvalidOperationException(
                "No se ha configurado la clave de cifrado ('Cifrado:ClaveBase64'). " +
                "Defínala mediante «dotnet user-secrets» o una variable de entorno; nunca en el código fuente.");
        }

        byte[] clave;
        try
        {
            clave = Convert.FromBase64String(claveBase64);
        }
        catch (FormatException excepcion)
        {
            throw new InvalidOperationException(
                "La clave de cifrado configurada no es Base64 válido.", excepcion);
        }

        if (clave.Length != TamanoClave)
        {
            throw new InvalidOperationException(
                $"La clave de cifrado debe medir exactamente {TamanoClave} bytes (256 bits); " +
                $"la configurada mide {clave.Length}.");
        }

        return clave;
    }

    /// <summary>
    /// Genera una clave AES-256 aleatoria en Base64. Se usa desde los guiones de
    /// aprovisionamiento para crear secretos nuevos sin escribirlos a mano.
    /// </summary>
    public static string GenerarClaveBase64()
    {
        var clave = RandomNumberGenerator.GetBytes(TamanoClave);
        var resultado = Convert.ToBase64String(clave);
        CryptographicOperations.ZeroMemory(clave);
        return resultado;
    }

    /// <summary>
    /// Calcula una huella corta y estable de la clave configurada, apta para registrar
    /// en los logs y verificar que servidor y datos usan la misma clave sin exponerla.
    /// </summary>
    /// <param name="claveBase64">Clave en Base64.</param>
    public static string CalcularHuellaClave(string claveBase64)
    {
        var clave = Convert.FromBase64String(claveBase64);
        var huella = SHA256.HashData(clave);
        CryptographicOperations.ZeroMemory(clave);

        // Solo los primeros 4 bytes: suficiente para distinguir claves, inútil para reconstruirla.
        return BinaryPrimitives.ReadUInt32BigEndian(huella).ToString("x8", System.Globalization.CultureInfo.InvariantCulture);
    }
}
