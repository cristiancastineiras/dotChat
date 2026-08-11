using System.Buffers.Binary;

namespace Chat.Infraestructura.Seguridad;

/// <summary>
/// Constantes y utilidades del formato de flujo cifrado por marcos.
/// </summary>
/// <remarks>
/// <para>Estructura del flujo:</para>
/// <code>
/// [ cabecera ]  magia(4) | versión(1) | tamañoMarco(4, big-endian) | semilla(8)
/// [ marco ]*    longitud(4, big-endian) | etiqueta(16) | textoCifrado(longitud)
/// </code>
/// <para>
/// Cada marco se sella con AES-256-GCM. El nonce se compone de la semilla aleatoria
/// del fichero y del número de marco, de modo que jamás se repite con la misma clave.
/// Los datos autenticados de cada marco incluyen el contexto de la aplicación, la
/// semilla, el número de marco y una marca de «este es el último».
/// </para>
/// <para>
/// De ahí salen tres garantías que un cifrado marco a marco ingenuo no da: no se puede
/// <b>reordenar</b> el contenido (el número va autenticado), no se puede <b>mezclar</b>
/// con el de otro fichero (la semilla va autenticada) y no se puede <b>truncar</b>
/// (el descifrado solo termina bien al encontrar un marco marcado como último).
/// </para>
/// </remarks>
internal static class FormatoFlujoCifrado
{
    /// <summary>Marca que identifica el formato: «dCF1» en ASCII.</summary>
    internal static ReadOnlySpan<byte> Magia => "dCF1"u8;

    /// <summary>Versión del formato de flujo.</summary>
    internal const byte Version = 1;

    /// <summary>Tamaño en bytes del contenido en claro de cada marco.</summary>
    internal const int TamanoMarco = 64 * 1024;

    /// <summary>Longitud de la semilla aleatoria por fichero.</summary>
    internal const int TamanoSemilla = 8;

    /// <summary>Longitud del nonce de AES-GCM: semilla más número de marco.</summary>
    internal const int TamanoNonce = 12;

    /// <summary>Longitud de la etiqueta de autenticación de AES-GCM.</summary>
    internal const int TamanoEtiqueta = 16;

    /// <summary>Longitud de la cabecera del flujo.</summary>
    internal const int TamanoCabecera = 4 + 1 + 4 + TamanoSemilla;

    /// <summary>Longitud de la cabecera de cada marco: longitud y etiqueta.</summary>
    internal const int TamanoCabeceraMarco = 4 + TamanoEtiqueta;

    /// <summary>
    /// Longitud de los datos autenticados de un marco: contexto, semilla, número y
    /// marca de último.
    /// </summary>
    /// <param name="longitudContexto">Longitud del contexto de la aplicación.</param>
    internal static int TamanoDatosAsociados(int longitudContexto)
        => longitudContexto + TamanoSemilla + 4 + 1;

    /// <summary>Escribe la cabecera del flujo en el búfer indicado.</summary>
    /// <param name="destino">Búfer de al menos <see cref="TamanoCabecera"/> bytes.</param>
    /// <param name="semilla">Semilla aleatoria del fichero.</param>
    internal static void EscribirCabecera(Span<byte> destino, ReadOnlySpan<byte> semilla)
    {
        Magia.CopyTo(destino);
        destino[4] = Version;
        BinaryPrimitives.WriteInt32BigEndian(destino[5..], TamanoMarco);
        semilla.CopyTo(destino[9..]);
    }

    /// <summary>Compone el nonce de un marco a partir de la semilla y su número.</summary>
    /// <param name="destino">Búfer de <see cref="TamanoNonce"/> bytes.</param>
    /// <param name="semilla">Semilla del fichero.</param>
    /// <param name="numeroMarco">Número de marco, empezando en cero.</param>
    internal static void ComponerNonce(Span<byte> destino, ReadOnlySpan<byte> semilla, int numeroMarco)
    {
        semilla.CopyTo(destino);
        BinaryPrimitives.WriteInt32BigEndian(destino[TamanoSemilla..], numeroMarco);
    }

    /// <summary>Compone los datos autenticados de un marco.</summary>
    /// <param name="destino">Búfer del tamaño que devuelve <see cref="TamanoDatosAsociados"/>.</param>
    /// <param name="contexto">Contexto de la aplicación.</param>
    /// <param name="semilla">Semilla del fichero.</param>
    /// <param name="numeroMarco">Número de marco.</param>
    /// <param name="esUltimo">Indica si es el marco que cierra el flujo.</param>
    internal static void ComponerDatosAsociados(
        Span<byte> destino,
        ReadOnlySpan<byte> contexto,
        ReadOnlySpan<byte> semilla,
        int numeroMarco,
        bool esUltimo)
    {
        contexto.CopyTo(destino);
        semilla.CopyTo(destino[contexto.Length..]);
        BinaryPrimitives.WriteInt32BigEndian(destino[(contexto.Length + TamanoSemilla)..], numeroMarco);
        destino[contexto.Length + TamanoSemilla + 4] = esUltimo ? (byte)1 : (byte)0;
    }
}
