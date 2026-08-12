using System.Security.Cryptography;
using System.Text;
using Chat.Aplicacion.Opciones;
using Chat.Infraestructura.Seguridad;
using Chat.Tests.Comun;

namespace Chat.Tests.Infraestructura;

/// <summary>
/// Pruebas del cifrado de mensajes en reposo (AES-256-GCM). Se comprueba que el
/// criptograma es confidencial, íntegro y que está ligado a esta aplicación.
/// </summary>
public sealed class PruebasServicioCifradorMensajes : IDisposable
{
    private readonly ServicioCifradorMensajes _cifrador = new(Opciones.De(Opciones.Cifrado()));

    /// <inheritdoc />
    public void Dispose() => _cifrador.Dispose();

    [Theory]
    [InlineData("hola")]
    [InlineData("")]
    [InlineData("con acentos: ñáéíóü")]
    [InlineData("con emojis: \U0001F525\U0001F44D")]
    public void LoCifradoVuelveIntacto(string original)
        => Assert.Equal(original, _cifrador.Descifrar(_cifrador.Cifrar(original)));

    [Fact]
    public void UnTextoLargoTambienVuelveIntacto()
    {
        var largo = new string('a', 100_000);

        Assert.Equal(largo, _cifrador.Descifrar(_cifrador.Cifrar(largo)));
    }

    [Fact]
    public void ElCriptogramaNoDejaVerElTextoOriginal()
    {
        var cifrado = _cifrador.Cifrar("secreto muy visible");

        Assert.DoesNotContain("secreto", cifrado, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DosMensajesIdenticosProducenCriptogramasDistintos()
    {
        // El nonce es aleatorio en cada operación: sin eso, quien mirase la base de
        // datos podría deducir qué mensajes se repiten sin descifrar ninguno.
        Assert.NotEqual(_cifrador.Cifrar("hola"), _cifrador.Cifrar("hola"));
    }

    [Fact]
    public void UnCriptogramaManipuladoNoSeDescifra()
    {
        var bytes = Convert.FromBase64String(_cifrador.Cifrar("hola"));
        bytes[^1] ^= 0xFF;

        Assert.ThrowsAny<CryptographicException>(() => _cifrador.Descifrar(Convert.ToBase64String(bytes)));
    }

    [Fact]
    public void UnCriptogramaConLaEtiquetaTocadaNoSeDescifra()
    {
        // La etiqueta va justo tras el nonce: alterarla es el ataque evidente contra
        // un cifrado autenticado.
        var bytes = Convert.FromBase64String(_cifrador.Cifrar("hola"));
        bytes[14] ^= 0x01;

        Assert.ThrowsAny<CryptographicException>(() => _cifrador.Descifrar(Convert.ToBase64String(bytes)));
    }

    [Fact]
    public void UnCriptogramaDeOtraVersionDeFormatoSeRechaza()
    {
        var bytes = Convert.FromBase64String(_cifrador.Cifrar("hola"));
        bytes[0] = 99;

        var excepcion = Assert.Throws<CryptographicException>(
            () => _cifrador.Descifrar(Convert.ToBase64String(bytes)));

        Assert.Contains("no soportada", excepcion.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void UnCriptogramaTruncadoSeRechaza()
    {
        var excepcion = Assert.Throws<CryptographicException>(
            () => _cifrador.Descifrar(Convert.ToBase64String([1, 2, 3])));

        Assert.Contains("truncado", excepcion.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void LoQueNoEsBase64SeRechazaComoCriptograma()
        => Assert.Throws<CryptographicException>(() => _cifrador.Descifrar("esto no es base64 !!!"));

    [Fact]
    public void OtraClaveNoAbreElCriptograma()
    {
        // Es la comprobación de que la clave importa de verdad: sin ella, el cifrado
        // sería decorativo.
        var cifrado = _cifrador.Cifrar("hola");

        using var otro = new ServicioCifradorMensajes(Opciones.De(new CifradoOptions
        {
            ClaveBase64 = ServicioCifradorMensajes.GenerarClaveBase64(),
            ContextoAsociado = "dotchat:prueba:v1"
        }));

        Assert.ThrowsAny<CryptographicException>(() => otro.Descifrar(cifrado));
    }

    [Fact]
    public void OtroContextoAsociadoNoAbreElCriptograma()
    {
        // El contexto va autenticado: liga el criptograma a esta aplicación y a este
        // uso, de modo que no se puede reutilizar en otro.
        var cifrado = _cifrador.Cifrar("hola");

        using var otro = new ServicioCifradorMensajes(Opciones.De(new CifradoOptions
        {
            ClaveBase64 = Opciones.ClaveCifradoBase64,
            ContextoAsociado = "otra-aplicacion"
        }));

        Assert.ThrowsAny<CryptographicException>(() => otro.Descifrar(cifrado));
    }

    [Fact]
    public void UnCriptogramaBinarioNoSePuedeHacerPasarPorUnoDeTexto()
    {
        // Texto y binario comparten clave pero no contexto: cruzarlos no cuela.
        var binario = Convert.ToBase64String(_cifrador.CifrarBinario([1, 2, 3]));

        Assert.ThrowsAny<CryptographicException>(() => _cifrador.Descifrar(binario));
    }

    [Fact]
    public void UnCriptogramaDeTextoNoSePuedeHacerPasarPorUnoBinario()
    {
        var texto = Convert.FromBase64String(_cifrador.Cifrar("hola"));

        Assert.False(_cifrador.IntentarDescifrarBinario(texto, out var resultado));
        Assert.Null(resultado);
    }

    [Fact]
    public void IntentarDescifrarDevuelveElTextoCuandoTodoEncaja()
    {
        Assert.True(_cifrador.IntentarDescifrar(_cifrador.Cifrar("hola"), out var texto));
        Assert.Equal("hola", texto);
    }

    [Theory]
    [InlineData("no es base64 !!!")]
    [InlineData("")]
    public void IntentarDescifrarNoLanzaAnteUnaEntradaInvalida(string entrada)
    {
        Assert.False(_cifrador.IntentarDescifrar(entrada, out var texto));
        Assert.Null(texto);
    }

    [Fact]
    public void ElContenidoBinarioVuelveIntacto()
    {
        var datos = new byte[512];
        RandomNumberGenerator.Fill(datos);

        Assert.True(_cifrador.IntentarDescifrarBinario(_cifrador.CifrarBinario(datos), out var recuperado));
        Assert.Equal(datos, recuperado);
    }

    [Fact]
    public void UnBinarioManipuladoNoSeDescifra()
    {
        var cifrado = _cifrador.CifrarBinario([1, 2, 3, 4]);
        cifrado[^1] ^= 0xFF;

        Assert.False(_cifrador.IntentarDescifrarBinario(cifrado, out var recuperado));
        Assert.Null(recuperado);
    }

    [Fact]
    public void CifrarUnTextoNuloSeRechaza()
        => Assert.Throws<ArgumentNullException>(() => _cifrador.Cifrar((string)null!));

    [Fact]
    public void LaClaveGeneradaMideTreintaYDosBytes()
    {
        var clave = ServicioCifradorMensajes.GenerarClaveBase64();

        Assert.Equal(32, Convert.FromBase64String(clave).Length);
        Assert.NotEqual(clave, ServicioCifradorMensajes.GenerarClaveBase64());
    }

    [Fact]
    public void LaHuellaDeLaClaveEsEstableYNoLaRevela()
    {
        // Sirve para verificar en los logs que servidor y datos usan la misma clave.
        var huella = ServicioCifradorMensajes.CalcularHuellaClave(Opciones.ClaveCifradoBase64);

        Assert.Equal(8, huella.Length);
        Assert.Equal(huella, ServicioCifradorMensajes.CalcularHuellaClave(Opciones.ClaveCifradoBase64));
        Assert.NotEqual(
            huella,
            ServicioCifradorMensajes.CalcularHuellaClave(ServicioCifradorMensajes.GenerarClaveBase64()));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void SinClaveConfiguradaElServicioNoArranca(string clave)
    {
        var excepcion = Assert.Throws<InvalidOperationException>(
            () => new ServicioCifradorMensajes(Opciones.De(new CifradoOptions { ClaveBase64 = clave })));

        Assert.Contains("Cifrado:ClaveBase64", excepcion.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void UnaClaveQueNoEsBase64ImpideArrancar()
        => Assert.Throws<InvalidOperationException>(
            () => new ServicioCifradorMensajes(Opciones.De(new CifradoOptions { ClaveBase64 = "no-base64-!!" })));

    [Fact]
    public void UnaClaveDeLongitudIncorrectaImpideArrancar()
    {
        var corta = Convert.ToBase64String(new byte[16]);

        var excepcion = Assert.Throws<InvalidOperationException>(
            () => new ServicioCifradorMensajes(Opciones.De(new CifradoOptions { ClaveBase64 = corta })));

        Assert.Contains("32 bytes", excepcion.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void UnCifradorLiberadoDejaDeAdmitirOperaciones()
    {
        var cifrador = new ServicioCifradorMensajes(Opciones.De(Opciones.Cifrado()));
        cifrador.Dispose();
        cifrador.Dispose();

        Assert.Throws<ObjectDisposedException>(() => cifrador.Cifrar("hola"));
    }
}

/// <summary>
/// Pruebas del cifrado en flujo, que es el que reciben los archivos adjuntos.
/// </summary>
/// <remarks>
/// El formato por marcos existe para dar tres garantías que un cifrado troceado
/// ingenuo no da: que el contenido no se puede reordenar, ni mezclar con el de otro
/// fichero, ni truncar. Cada una tiene aquí su prueba.
/// </remarks>
public sealed class PruebasFlujoCifrado : IDisposable
{
    /// <summary>Tamaño del marco del formato, del que dependen los casos límite.</summary>
    private const int TamanoMarco = 64 * 1024;

    private readonly ServicioCifradorMensajes _cifrador = new(Opciones.De(Opciones.Cifrado()));

    /// <inheritdoc />
    public void Dispose() => _cifrador.Dispose();

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(1024)]
    [InlineData(TamanoMarco - 1)]
    [InlineData(TamanoMarco)]
    [InlineData(TamanoMarco + 1)]
    [InlineData(TamanoMarco * 3)]
    [InlineData((TamanoMarco * 2) + 517)]
    public async Task ElContenidoVuelveIntactoSeaCualSeaSuTamano(int tamano)
    {
        var original = Contenido(tamano);

        Assert.Equal(original, await IdaYVueltaAsync(original));
    }

    [Fact]
    public async Task ElTamanoAnunciadoAlAlmacenCoincideConElRealmenteProducido()
    {
        // El almacén de objetos necesita saber de antemano cuántos bytes va a recibir,
        // y el flujo cifrado se genera sobre la marcha: si la cuenta no cuadrara, la
        // subida fallaría o quedaría truncada.
        foreach (var tamano in new[] { 0, 1, 5_000, TamanoMarco, TamanoMarco + 1, TamanoMarco * 2 })
        {
            await using var claro = new MemoryStream(Contenido(tamano), writable: false);
            await using var cifrado = _cifrador.Cifrar(claro);
            using var destino = new MemoryStream();
            await cifrado.CopyToAsync(destino);

            Assert.Equal(_cifrador.CalcularTamanoCifrado(tamano), destino.Length);
        }
    }

    [Fact]
    public void ElTamanoCifradoNoAdmiteValoresNegativos()
        => Assert.Throws<ArgumentOutOfRangeException>(() => _cifrador.CalcularTamanoCifrado(-1));

    [Fact]
    public async Task UnContenidoTruncadoNoSeDaPorBueno()
    {
        // Solo se termina bien al encontrar el marco marcado como último: una
        // transferencia cortada falla en lugar de entregar medio fichero.
        var cifrado = await CifrarAsync(Contenido(TamanoMarco * 2));

        await using var origen = new MemoryStream(cifrado[..(cifrado.Length / 2)], writable: false);
        await Assert.ThrowsAsync<CryptographicException>(() => DescifrarAsync(origen));
    }

    [Fact]
    public async Task QuitarElUltimoMarcoNoSeDaPorBueno()
    {
        var cifrado = await CifrarAsync(Contenido(100));

        // Se recorta justo el marco final, que es lo que cierra el flujo.
        await using var origen = new MemoryStream(cifrado[..^1], writable: false);
        await Assert.ThrowsAsync<CryptographicException>(() => DescifrarAsync(origen));
    }

    [Fact]
    public async Task UnContenidoManipuladoNoSeDescifra()
    {
        var cifrado = await CifrarAsync(Contenido(1024));
        cifrado[^5] ^= 0xFF;

        await using var origen = new MemoryStream(cifrado, writable: false);
        await Assert.ThrowsAsync<CryptographicException>(() => DescifrarAsync(origen));
    }

    [Fact]
    public async Task NoSePuedenMezclarLosMarcosDeDosFicheros()
    {
        // La semilla va autenticada en cada marco: los de un fichero no valen en otro,
        // aunque los haya cifrado la misma clave.
        var primero = await CifrarAsync(Contenido(TamanoMarco * 2));
        var segundo = await CifrarAsync(Contenido(TamanoMarco * 2));

        // Se conserva la cabecera del primero y se le pega el cuerpo del segundo.
        const int tamanoCabecera = 4 + 1 + 4 + 8;
        var mezcla = new byte[primero.Length];
        primero.AsSpan(0, tamanoCabecera).CopyTo(mezcla);
        segundo.AsSpan(tamanoCabecera).CopyTo(mezcla.AsSpan(tamanoCabecera));

        await using var origen = new MemoryStream(mezcla, writable: false);
        await Assert.ThrowsAsync<CryptographicException>(() => DescifrarAsync(origen));
    }

    [Fact]
    public async Task UnaCabeceraDesconocidaSeRechaza()
    {
        var cifrado = await CifrarAsync(Contenido(10));
        cifrado[0] = (byte)'X';

        await using var origen = new MemoryStream(cifrado, writable: false);
        var excepcion = await Assert.ThrowsAsync<CryptographicException>(() => DescifrarAsync(origen));

        Assert.Contains("formato de flujo cifrado", excepcion.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnaVersionDeFormatoDesconocidaSeRechaza()
    {
        var cifrado = await CifrarAsync(Contenido(10));
        cifrado[4] = 99;

        await using var origen = new MemoryStream(cifrado, writable: false);
        var excepcion = await Assert.ThrowsAsync<CryptographicException>(() => DescifrarAsync(origen));

        Assert.Contains("no soportada", excepcion.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnTamanoDeMarcoDistintoSeRechaza()
    {
        var cifrado = await CifrarAsync(Contenido(10));
        cifrado[5] = 0xFF;

        await using var origen = new MemoryStream(cifrado, writable: false);
        await Assert.ThrowsAsync<CryptographicException>(() => DescifrarAsync(origen));
    }

    [Fact]
    public async Task OtraClaveNoAbreElFlujo()
    {
        var cifrado = await CifrarAsync(Contenido(1024));

        using var otro = new ServicioCifradorMensajes(Opciones.De(new CifradoOptions
        {
            ClaveBase64 = ServicioCifradorMensajes.GenerarClaveBase64(),
            ContextoAsociado = "dotchat:prueba:v1"
        }));

        await using var origen = new MemoryStream(cifrado, writable: false);
        await using var claro = otro.Descifrar(origen);
        using var destino = new MemoryStream();

        await Assert.ThrowsAsync<CryptographicException>(() => claro.CopyToAsync(destino));
    }

    [Fact]
    public async Task ElFlujoCifradoNoAdmiteEscrituraNiBusqueda()
    {
        await using var claro = new MemoryStream(Contenido(10), writable: false);
        await using var cifrado = _cifrador.Cifrar(claro);

        Assert.True(cifrado.CanRead);
        Assert.False(cifrado.CanSeek);
        Assert.False(cifrado.CanWrite);
        Assert.Throws<NotSupportedException>(() => cifrado.Seek(0, SeekOrigin.Begin));
        Assert.Throws<NotSupportedException>(() => cifrado.Write([1], 0, 1));
        Assert.Throws<NotSupportedException>(() => cifrado.Length);
        Assert.Throws<NotSupportedException>(() => cifrado.SetLength(1));
    }

    [Fact]
    public async Task LeerDeUnFlujoYaLiberadoFalla()
    {
        var claro = new MemoryStream(Contenido(10), writable: false);
        var cifrado = _cifrador.Cifrar(claro);
        await cifrado.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
        {
            var leidos = await cifrado.ReadAsync(new byte[4]);
            Assert.Equal(0, leidos);
        });
    }

    [Fact]
    public async Task ElFlujoDeOrigenSeLiberaConElCifrador()
    {
        // Quien consume el flujo cifrado no conoce el de origen: si no se liberara con
        // él, cada subida dejaría un descriptor abierto.
        var claro = new MemoryStream(Contenido(10), writable: false);
        var cifrado = _cifrador.Cifrar(claro);

        await cifrado.DisposeAsync();

        Assert.Throws<ObjectDisposedException>(() => claro.ReadByte());
    }

    [Fact]
    public async Task UnOrigenQueEntregaElContenidoAGoteoTambienSeCifraEntero()
    {
        // Un flujo de red devuelve menos bytes de los pedidos sin haber terminado: hay
        // que insistir hasta completar el marco.
        var original = Contenido(TamanoMarco + 1234);

        await using var lento = new FlujoAGoteo(original, 7);
        await using var cifrado = _cifrador.Cifrar(lento);
        using var intermedio = new MemoryStream();
        await cifrado.CopyToAsync(intermedio);

        await using var origen = new MemoryStream(intermedio.ToArray(), writable: false);
        Assert.Equal(original, await DescifrarAsync(origen));
    }

    /// <summary>Genera un contenido reproducible del tamaño indicado.</summary>
    /// <param name="tamano">Número de bytes.</param>
    private static byte[] Contenido(int tamano)
    {
        var datos = new byte[tamano];

        for (var i = 0; i < tamano; i++)
        {
            datos[i] = (byte)(i % 251);
        }

        return datos;
    }

    /// <summary>Cifra un contenido y devuelve el criptograma completo.</summary>
    /// <param name="original">Contenido en claro.</param>
    private async Task<byte[]> CifrarAsync(byte[] original)
    {
        await using var claro = new MemoryStream(original, writable: false);
        await using var cifrado = _cifrador.Cifrar(claro);
        using var destino = new MemoryStream();
        await cifrado.CopyToAsync(destino);

        return destino.ToArray();
    }

    /// <summary>Descifra un flujo completo.</summary>
    /// <param name="origen">Flujo con el criptograma.</param>
    private async Task<byte[]> DescifrarAsync(Stream origen)
    {
        await using var claro = _cifrador.Descifrar(origen);
        using var destino = new MemoryStream();
        await claro.CopyToAsync(destino);

        return destino.ToArray();
    }

    /// <summary>Ida y vuelta completa por el cifrador de flujo.</summary>
    /// <param name="original">Contenido en claro.</param>
    private async Task<byte[]> IdaYVueltaAsync(byte[] original)
    {
        await using var origen = new MemoryStream(await CifrarAsync(original), writable: false);
        return await DescifrarAsync(origen);
    }

    /// <summary>Flujo que entrega el contenido en trozos pequeños, como haría la red.</summary>
    /// <param name="contenido">Contenido a entregar.</param>
    /// <param name="porLectura">Bytes máximos que devuelve en cada lectura.</param>
    private sealed class FlujoAGoteo(byte[] contenido, int porLectura) : Stream
    {
        private int _posicion;

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
        public override int Read(byte[] bufer, int desplazamiento, int cantidad)
        {
            var entregados = Math.Min(Math.Min(porLectura, cantidad), contenido.Length - _posicion);
            contenido.AsSpan(_posicion, entregados).CopyTo(bufer.AsSpan(desplazamiento, entregados));
            _posicion += entregados;

            return entregados;
        }

        /// <inheritdoc />
        public override ValueTask<int> ReadAsync(Memory<byte> destino, CancellationToken cancelacion = default)
        {
            var entregados = Math.Min(Math.Min(porLectura, destino.Length), contenido.Length - _posicion);
            contenido.AsSpan(_posicion, entregados).CopyTo(destino.Span);
            _posicion += entregados;

            return ValueTask.FromResult(entregados);
        }

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
    }
}
