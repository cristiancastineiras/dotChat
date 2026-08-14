using Chat.Infraestructura.Audio;

namespace Chat.Tests.Infraestructura;

/// <summary>
/// Pruebas del reconocimiento de audio. Igual que con las imágenes, lo que importa es
/// que se decide mirando los bytes y no el nombre ni el tipo declarado, y que el flujo
/// queda donde estaba para que lo que se almacene después no vaya truncado.
/// </summary>
public sealed class PruebasProcesadorAudio
{
    private static readonly ProcesadorAudioSniffer Procesador = new();

    [Fact]
    public async Task UnaCabeceraWebmSeReconoceComoAudio()
    {
        // Firma EBML de MediaRecorder al grabar sin pedir un tipo concreto.
        await using var origen = Flujo(0x1A, 0x45, 0xDF, 0xA3, 0x01, 0x02, 0x03);

        Assert.True(await Procesador.EsAudioAsync(origen));
    }

    [Fact]
    public async Task UnaCabeceraOggSeReconoceComoAudio()
    {
        await using var origen = Flujo("OggS"u8.ToArray());

        Assert.True(await Procesador.EsAudioAsync(origen));
    }

    [Fact]
    public async Task UnaCabeceraWavSeReconoceComoAudio()
    {
        var cabecera = new byte[12];
        "RIFF"u8.CopyTo(cabecera);
        "WAVE"u8.CopyTo(cabecera.AsSpan(8));

        await using var origen = Flujo(cabecera);

        Assert.True(await Procesador.EsAudioAsync(origen));
    }

    [Fact]
    public async Task UnMp3ConEtiquetaId3SeReconoceComoAudio()
    {
        await using var origen = Flujo("ID3"u8.ToArray());

        Assert.True(await Procesador.EsAudioAsync(origen));
    }

    [Fact]
    public async Task UnMp3SinEtiquetaSeReconocePorLaTramaMpeg()
    {
        await using var origen = Flujo(0xFF, 0xFB, 0x90, 0x00);

        Assert.True(await Procesador.EsAudioAsync(origen));
    }

    [Fact]
    public async Task UnM4aSeReconocePorSuCabeceraFtyp()
    {
        var cabecera = new byte[8];
        cabecera[3] = 0x18; // tamaño de la caja, irrelevante para la firma.
        "ftyp"u8.CopyTo(cabecera.AsSpan(4));

        await using var origen = Flujo(cabecera);

        Assert.True(await Procesador.EsAudioAsync(origen));
    }

    [Fact]
    public async Task UnContenidoCualquieraNoSeReconoceComoAudio()
    {
        await using var origen = Flujo("no es audio, es texto plano"u8.ToArray());

        Assert.False(await Procesador.EsAudioAsync(origen));
    }

    [Fact]
    public async Task ComprobarSiEsAudioDevuelveElFlujoDondeEstaba()
    {
        await using var origen = Flujo("OggS"u8.ToArray());
        origen.Position = 0;

        await Procesador.EsAudioAsync(origen);

        Assert.Equal(0, origen.Position);
    }

    [Fact]
    public async Task UnFlujoSinBusquedaSeTrataComoArchivoCualquiera()
    {
        await using var envoltura = new FlujoSinBusqueda(Flujo("OggS"u8.ToArray()));

        Assert.False(await Procesador.EsAudioAsync(envoltura));
    }

    private static MemoryStream Flujo(params byte[] contenido) => new(contenido, writable: false);

    /// <summary>Envoltura que oculta la capacidad de búsqueda de un flujo de memoria.</summary>
    private sealed class FlujoSinBusqueda(Stream interno) : Stream
    {
        public override bool CanRead => interno.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) => interno.Read(buffer, offset, count);

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            => interno.ReadAsync(buffer, cancellationToken);

        public override void Flush() => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                interno.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
