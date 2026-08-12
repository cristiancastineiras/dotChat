using Chat.Aplicacion.Mensajeria;

namespace Chat.Tests.Mensajeria;

/// <summary>
/// Pruebas de la deducción de tipos MIME. El tipo que declara el cliente no se usa
/// nunca: se deduce de la extensión ya saneada y, ante la duda, se devuelve el
/// genérico, que ningún visor ejecuta.
/// </summary>
public sealed class PruebasTiposMime
{
    [Theory]
    [InlineData("foto.png", "image/png")]
    [InlineData("foto.jpg", "image/jpeg")]
    [InlineData("foto.jpeg", "image/jpeg")]
    [InlineData("informe.pdf", "application/pdf")]
    [InlineData("notas.txt", "text/plain")]
    [InlineData("datos.json", "application/json")]
    [InlineData("copia.zip", "application/zip")]
    [InlineData("cancion.mp3", "audio/mpeg")]
    public void SeReconocenLasExtensionesHabituales(string nombre, string esperado)
        => Assert.Equal(esperado, TiposMime.DeducirDe(nombre));

    [Theory]
    [InlineData("FOTO.PNG")]
    [InlineData("Foto.Png")]
    public void LaExtensionSeReconoceSinDistinguirMayusculas(string nombre)
        => Assert.Equal("image/png", TiposMime.DeducirDe(nombre));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("sinextension")]
    [InlineData("acaba.en.punto.")]
    [InlineData("desconocida.qwerty")]
    public void LoQueNoSeReconoceSeSirveComoGenerico(string? nombre)
        => Assert.Equal(TiposMime.Generico, TiposMime.DeducirDe(nombre));

    [Fact]
    public void UnNombreOcultoSinExtensionRealTambienEsGenerico()
    {
        // «.gitignore» no tiene extensión: el punto inicial es parte del nombre.
        Assert.Equal(TiposMime.Generico, TiposMime.DeducirDe(".gitignore"));
    }

    [Fact]
    public void SeUsaLaUltimaExtensionDeUnNombreCompuesto()
    {
        // Es la defensa contra «factura.pdf.exe»: manda lo último, no lo que parece.
        Assert.Equal(TiposMime.Generico, TiposMime.DeducirDe("factura.pdf.exe"));
        Assert.Equal("application/pdf", TiposMime.DeducirDe("copia.zip.pdf"));
    }
}
