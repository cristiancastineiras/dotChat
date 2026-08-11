using Chat.Aplicacion.Validacion;
using Chat.Dominio.Excepciones;

namespace Chat.Tests.Validacion;

/// <summary>
/// Pruebas del saneamiento de entrada, centradas en el equilibrio entre dejar pasar
/// los emojis compuestos y seguir descartando los caracteres invisibles que sirven
/// para suplantar identidades.
/// </summary>
public sealed class PruebasValidadorEntrada
{
    /// <summary>Unión de secuencia: el pegamento de los emojis compuestos.</summary>
    private const string Union = "\u200D";

    /// <summary>Anulación de escritura de derecha a izquierda, usada para camuflar texto.</summary>
    private const string AnulacionDireccion = "\u202E";

    /// <summary>Familia de tres personas, unida con dos uniones de secuencia.</summary>
    private static readonly string Familia =
        $"\U0001F468{Union}\U0001F469{Union}\U0001F467";

    [Fact]
    public void ElTextoDeUnMensajeConservaLosEmojisCompuestos()
    {
        var resultado = ValidadorEntrada.ValidarTextoMensaje($"mira {Familia}", 2000);

        Assert.Equal($"mira {Familia}", resultado);
    }

    [Fact]
    public void ElTextoDeUnMensajeConservaLosEmojisConSelectorDeVariacion()
    {
        // El corazón rojo es «corazón negro» más un selector de variación; si el
        // selector se perdiera, saldría en blanco y negro.
        const string corazon = "\u2764\uFE0F";

        Assert.Equal(corazon, ValidadorEntrada.ValidarTextoMensaje(corazon, 2000));
    }

    [Fact]
    public void ElTextoDeUnMensajeSigueDescartandoLosInvisiblesPeligrosos()
    {
        // Donde había un invisible queda un corte de palabra, no una unión: así
        // «ad‮min» se ve como «ad min» y no se convierte en «admin».
        var resultado = ValidadorEntrada.ValidarTextoMensaje($"hola{AnulacionDireccion}mundo", 2000);

        Assert.Equal("hola mundo", resultado);
    }

    [Fact]
    public void UnNombreDeUsuarioNoAdmiteLaUnionDeSecuencia()
    {
        // Fuera del cuerpo de un mensaje, la unión de secuencia se descarta como
        // cualquier otro invisible y deja un separador en su lugar. El patrón de
        // nombre de usuario rechaza después el espacio resultante.
        Assert.Equal("ana eva", ValidadorEntrada.Sanear($"ana{Union}eva"));
        Assert.Throws<ExcepcionValidacion>(() => ValidadorEntrada.ValidarNombreUsuario($"ana{Union}eva"));
    }

    [Fact]
    public void UnaUnionDeSecuenciaSueltaAlPrincipioNoSeConserva()
    {
        // Al principio no une nada: solo sería un carácter invisible de adorno.
        Assert.Equal("hola", ValidadorEntrada.ValidarTextoMensaje($"{Union}hola", 2000));
    }

    [Fact]
    public void UnMensajeVacioSeRechaza()
        => Assert.Throws<ExcepcionValidacion>(() => ValidadorEntrada.ValidarTextoMensaje("   ", 2000));

    [Fact]
    public void UnPieDeFotoVacioSeAceptaComoAusente()
        => Assert.Null(ValidadorEntrada.ValidarPieDeFoto("   ", 2000));

    [Fact]
    public void UnPieDeFotoDemasiadoLargoSeRechaza()
        => Assert.Throws<ExcepcionValidacion>(() => ValidadorEntrada.ValidarPieDeFoto(new string('a', 51), 50));

    [Theory]
    [InlineData(@"..\..\etc\passwd", "passwd")]
    [InlineData(@"C:\Mis fotos\perro.jpg", "perro.jpg")]
    [InlineData("/var/tmp/captura.png", "captura.png")]
    [InlineData("informe*final?.png", "informe_final_.png")]
    [InlineData("   ", "imagen")]
    [InlineData("...", "imagen")]
    public void ElNombreDeArchivoSeReduceAlNombreYSeSanea(string entrada, string esperado)
        => Assert.Equal(esperado, ValidadorEntrada.ValidarNombreArchivo(entrada));

    [Fact]
    public void ElNombreDeArchivoSeRecortaAlMaximo()
    {
        var largo = new string('a', ValidadorEntrada.LongitudMaximaNombreArchivo + 50) + ".png";

        var resultado = ValidadorEntrada.ValidarNombreArchivo(largo);

        Assert.Equal(ValidadorEntrada.LongitudMaximaNombreArchivo, resultado.Length);
    }
}
