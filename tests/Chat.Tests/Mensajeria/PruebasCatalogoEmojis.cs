using Chat.Aplicacion.Mensajeria;

namespace Chat.Tests.Mensajeria;

/// <summary>
/// Pruebas del catálogo de emojis.
/// </summary>
/// <remarks>
/// El catálogo comprueba sus propios duplicados al cargarse el tipo, lo que convierte
/// un error de edición en una excepción en la primera petición que expanda un atajo.
/// Estas pruebas adelantan ese momento a la compilación.
/// </remarks>
public sealed class PruebasCatalogoEmojis
{
    [Fact]
    public void ElCatalogoSeCargaSinAtajosDuplicados()
    {
        // Basta con tocar el índice: si hubiera un nombre o alias repetido, el
        // inicializador estático lanzaría al construirlo.
        var total = CatalogoEmojis.TotalAtajos;

        Assert.True(total >= CatalogoEmojis.Entradas.Count);
    }

    [Theory]
    [InlineData(":fuego:", "\U0001F525")]
    [InlineData(":fire:", "\U0001F525")]
    [InlineData("todo listo :pulgar:", "todo listo \U0001F44D")]
    [InlineData(":fuego::fuego:", "\U0001F525\U0001F525")]
    public void ExpandirSustituyeLosAtajosConocidos(string entrada, string esperado)
        => Assert.Equal(esperado, CatalogoEmojis.Expandir(entrada));

    [Theory]
    [InlineData("Nos vemos a las 12:30:45")]
    [InlineData(@"El registro está en C:\logs\salida.txt")]
    [InlineData(":noexiste:")]
    [InlineData("proporción 3:4")]
    [InlineData("")]
    public void ExpandirDejaIntactoLoQueNoEsUnAtajo(string entrada)
        => Assert.Equal(entrada, CatalogoEmojis.Expandir(entrada));

    [Fact]
    public void ExpandirEncuentraElAtajoAunqueElPrimerDelimitadorSeaFalso()
    {
        // El primer signo de dos puntos no abre nada, pero el segundo sí: no debe
        // perderse la sustitución por haber empezado a mirar en el sitio equivocado.
        Assert.Equal("12:30 \U0001F525", CatalogoEmojis.Expandir("12:30 :fuego:"));
    }

    [Fact]
    public void ExpandirNoDistingueMayusculas()
        => Assert.Equal("\U0001F525", CatalogoEmojis.Expandir(":FUEGO:"));

    [Fact]
    public void TodasLasEntradasTienenNombreSimboloYCategoria()
    {
        foreach (var entrada in CatalogoEmojis.Entradas)
        {
            Assert.False(string.IsNullOrWhiteSpace(entrada.Nombre));
            Assert.False(string.IsNullOrWhiteSpace(entrada.Simbolo));
            Assert.False(string.IsNullOrWhiteSpace(entrada.Categoria));
            Assert.Equal(entrada.Nombre.ToLowerInvariant(), entrada.Nombre);
        }
    }

    [Fact]
    public void CadaEntradaEsAlcanzablePorSuNombre()
    {
        foreach (var entrada in CatalogoEmojis.Entradas)
        {
            Assert.True(CatalogoEmojis.TryObtener(entrada.Nombre, out var simbolo));
            Assert.Equal(entrada.Simbolo, simbolo);
        }
    }
}
