using Chat.Dominio.Excepciones;

namespace Chat.Tests.Dominio;

/// <summary>
/// Pruebas de las excepciones de dominio. Su jerarquía no es decorativa: la capa de
/// presentación se apoya en ella para traducir cada error a su código HTTP sin
/// conocer los tipos concretos.
/// </summary>
public sealed class PruebasExcepciones
{
    public static TheoryData<ExcepcionDominio> Todas =>
    [
        new ExcepcionAutenticacion("credenciales"),
        new ExcepcionAutorizacion("prohibido"),
        new ExcepcionConflicto("duplicado"),
        new ExcepcionNoEncontrado("no existe"),
        new ExcepcionValidacion("campo", "no vale")
    ];

    [Theory]
    [MemberData(nameof(Todas))]
    public void TodosLosErroresEsperadosDerivanDeLaExcepcionDeDominio(ExcepcionDominio excepcion)
        => Assert.IsAssignableFrom<ExcepcionDominio>(excepcion);

    [Fact]
    public void ElMensajeEstandarDeNoEncontradoNombraElRecursoYSuIdentificador()
    {
        var id = Guid.CreateVersion7();

        var excepcion = ExcepcionNoEncontrado.Para("La sala", id);

        Assert.Equal($"La sala con identificador '{id}' no existe.", excepcion.Message);
    }

    [Fact]
    public void UnErrorDeValidacionDeUnSoloCampoSeAgrupaPorEseCampo()
    {
        var excepcion = new ExcepcionValidacion("nombreUsuario", "Formato incorrecto.");

        Assert.Equal("Formato incorrecto.", excepcion.Message);
        Assert.Equal(["Formato incorrecto."], excepcion.Errores["nombreUsuario"]);
    }

    [Fact]
    public void UnErrorDeValidacionDeVariosCamposLlevaUnMensajeGenerico()
    {
        var errores = new Dictionary<string, string[]>
        {
            ["clave"] = ["Demasiado corta.", "Falta un dígito."],
            ["email"] = ["No es válido."]
        };

        var excepcion = new ExcepcionValidacion(errores);

        Assert.Equal("Los datos proporcionados no son válidos.", excepcion.Message);
        Assert.Equal(2, excepcion.Errores.Count);
        Assert.Equal(2, excepcion.Errores["clave"].Length);
    }
}
