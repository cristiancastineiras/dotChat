using Chat.Aplicacion.Abstracciones;

namespace Chat.Infraestructura.Audio;

/// <summary>
/// Implementación de <see cref="IProcesadorAudio"/> por firma binaria.
/// </summary>
/// <remarks>
/// No hace falta una librería de códecs para esto: basta con mirar los primeros bytes,
/// que es lo que identifica de forma inequívoca al contenedor (no al códec de dentro).
/// Cubre lo que produce <c>MediaRecorder</c> en un navegador —WebM y Ogg, ambos con
/// Opus— y los formatos de audio más comunes que alguien podría adjuntar sueltos.
/// </remarks>
public sealed class ProcesadorAudioSniffer : IProcesadorAudio
{
    /// <summary>Bytes que hacen falta leer para reconocer cualquiera de las firmas.</summary>
    private const int LongitudCabecera = 12;

    /// <inheritdoc />
    public async Task<bool> EsAudioAsync(Stream origen, CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(origen);

        if (!origen.CanSeek)
        {
            // Igual que con las imágenes: sin poder rebobinar, leer la cabecera se
            // comería el contenido que luego hay que almacenar. Se trata como archivo
            // cualquiera, que es lo seguro.
            return false;
        }

        var posicion = origen.Position;
        var cabecera = new byte[LongitudCabecera];

        try
        {
            var leidos = await origen.ReadAtLeastAsync(cabecera, LongitudCabecera, throwOnEndOfStream: false, cancelacion)
                .ConfigureAwait(false);

            return EsFirmaReconocida(cabecera.AsSpan(0, leidos));
        }
        finally
        {
            origen.Position = posicion;
        }
    }

    /// <summary>Compara los primeros bytes con las firmas de contenedor conocidas.</summary>
    /// <param name="cabecera">Bytes leídos del principio del flujo.</param>
    private static bool EsFirmaReconocida(ReadOnlySpan<byte> cabecera)
    {
        // WebM/Matroska (EBML): lo que produce Chrome/Edge/Firefox al grabar con
        // MediaRecorder sin pedir un tipo concreto.
        if (Comienza(cabecera, [0x1A, 0x45, 0xDF, 0xA3]))
        {
            return true;
        }

        // Ogg (contenedor típico de Opus/Vorbis en Firefox).
        if (Comienza(cabecera, "OggS"u8))
        {
            return true;
        }

        // WAV: cabecera RIFF con el subtipo WAVE a partir del byte 8.
        if (cabecera.Length >= 12 && Comienza(cabecera, "RIFF"u8) && cabecera[8..12].SequenceEqual("WAVE"u8))
        {
            return true;
        }

        // MP3 con etiqueta ID3v2, o cabecera de trama MPEG sin etiqueta (los dos bytes
        // de sincronismo son 11 unos seguidos del identificador de versión/capa).
        if (Comienza(cabecera, "ID3"u8))
        {
            return true;
        }

        if (cabecera.Length >= 2 && cabecera[0] == 0xFF && (cabecera[1] & 0xE0) == 0xE0)
        {
            return true;
        }

        // Contenedor MP4/M4A: los cuatro bytes en la posición 4 son siempre "ftyp".
        if (cabecera.Length >= 8 && cabecera[4..8].SequenceEqual("ftyp"u8))
        {
            return true;
        }

        return false;
    }

    /// <summary>Indica si el contenido empieza por la secuencia de bytes dada.</summary>
    private static bool Comienza(ReadOnlySpan<byte> contenido, ReadOnlySpan<byte> firma)
        => contenido.Length >= firma.Length && contenido[..firma.Length].SequenceEqual(firma);
}
