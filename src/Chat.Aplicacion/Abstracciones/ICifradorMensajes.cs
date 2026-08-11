namespace Chat.Aplicacion.Abstracciones;

/// <summary>
/// Cifra y descifra el contenido de los mensajes antes de persistirlos.
/// La implementación debe usar cifrado autenticado (AES-256-GCM) para garantizar
/// confidencialidad e integridad del texto almacenado.
/// </summary>
public interface ICifradorMensajes
{
    /// <summary>Cifra un texto en claro.</summary>
    /// <param name="textoPlano">Contenido original del mensaje.</param>
    /// <returns>Criptograma en Base64, con nonce y etiqueta de autenticación incluidos.</returns>
    string Cifrar(string textoPlano);

    /// <summary>Descifra un criptograma previamente generado por <see cref="Cifrar"/>.</summary>
    /// <param name="textoCifrado">Criptograma en Base64.</param>
    /// <returns>Texto en claro.</returns>
    /// <exception cref="System.Security.Cryptography.CryptographicException">
    /// Si el criptograma fue manipulado o se cifró con otra clave.
    /// </exception>
    string Descifrar(string textoCifrado);

    /// <summary>
    /// Intenta descifrar sin lanzar excepciones. Útil al listar historiales en los que
    /// pudiera haber restos cifrados con una clave anterior.
    /// </summary>
    /// <param name="textoCifrado">Criptograma en Base64.</param>
    /// <param name="textoPlano">Texto en claro resultante, o <c>null</c> si falla.</param>
    /// <returns><c>true</c> si el descifrado fue correcto.</returns>
    bool IntentarDescifrar(string textoCifrado, out string? textoPlano);

    /// <summary>
    /// Cifra un contenido binario, como la imagen de un adjunto.
    /// </summary>
    /// <remarks>
    /// Usa la misma clave que el texto pero un contexto asociado distinto, de modo que
    /// un criptograma de mensaje no pueda hacerse pasar por el de una imagen ni al revés.
    /// </remarks>
    /// <param name="datosPlanos">Contenido original.</param>
    /// <returns>Criptograma binario, con nonce y etiqueta de autenticación incluidos.</returns>
    byte[] CifrarBinario(ReadOnlySpan<byte> datosPlanos);

    /// <summary>Intenta descifrar un contenido binario sin lanzar excepciones.</summary>
    /// <param name="datosCifrados">Criptograma generado por <see cref="CifrarBinario"/>.</param>
    /// <param name="datosPlanos">Contenido en claro resultante, o <c>null</c> si falla.</param>
    /// <returns><c>true</c> si el descifrado fue correcto.</returns>
    bool IntentarDescifrarBinario(byte[] datosCifrados, out byte[]? datosPlanos);
}
