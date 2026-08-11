namespace Chat.Aplicacion.Abstracciones;

/// <summary>
/// Cifra y descifra contenidos que no caben cómodamente en memoria, envolviendo un
/// flujo en otro.
/// </summary>
/// <remarks>
/// <para>
/// El cifrador de mensajes trabaja sobre búferes completos, que es lo razonable para
/// un texto de dos mil caracteres. Un archivo de veinte megas es otra cosa: cargarlo
/// entero para cifrarlo, y otra vez para descifrarlo, multiplicaría la memoria del
/// servidor por el número de transferencias simultáneas.
/// </para>
/// <para>
/// La implementación trocea el contenido y sella cada trozo por separado, de modo que
/// se puede leer y escribir de principio a fin sin conocer el tamaño total.
/// </para>
/// </remarks>
public interface ICifradorFlujo
{
    /// <summary>
    /// Envuelve un flujo en claro y devuelve otro del que se lee el contenido cifrado.
    /// </summary>
    /// <param name="claro">Flujo de origen, legible.</param>
    /// <returns>Flujo de solo lectura con el criptograma; al liberarlo se libera el de origen.</returns>
    Stream Cifrar(Stream claro);

    /// <summary>
    /// Envuelve un flujo cifrado y devuelve otro del que se lee el contenido en claro.
    /// </summary>
    /// <param name="cifrado">Flujo de origen, legible.</param>
    /// <returns>Flujo de solo lectura con el contenido descifrado.</returns>
    /// <exception cref="System.Security.Cryptography.CryptographicException">
    /// Al leer, si el criptograma fue manipulado, truncado, reordenado o se cifró con
    /// otra clave.
    /// </exception>
    Stream Descifrar(Stream cifrado);

    /// <summary>
    /// Calcula cuánto ocupará el criptograma de un contenido del tamaño indicado.
    /// Sirve para anunciar la longitud al almacén de objetos antes de empezar a subir.
    /// </summary>
    /// <param name="tamanoEnClaro">Tamaño del contenido original, en bytes.</param>
    long CalcularTamanoCifrado(long tamanoEnClaro);
}
