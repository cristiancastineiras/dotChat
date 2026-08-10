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
}
