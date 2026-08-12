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

    [Theory]
    [InlineData("ana")]
    [InlineData("ana.eva")]
    [InlineData("ana_eva-99")]
    [InlineData("A1b")]
    public void UnNombreDeUsuarioBienFormadoSeConserva(string entrada)
        => Assert.Equal(entrada, ValidadorEntrada.ValidarNombreUsuario(entrada));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ab")]
    [InlineData("ana eva")]
    [InlineData("ana@eva")]
    [InlineData("añana")]
    // «а» cirílica: se ve igual que la latina y es la base de las suplantaciones
    // por homógrafos, así que el patrón ASCII la rechaza.
    [InlineData("аna")]
    public void UnNombreDeUsuarioMalFormadoSeRechaza(string? entrada)
        => Assert.Throws<ExcepcionValidacion>(() => ValidadorEntrada.ValidarNombreUsuario(entrada));

    [Fact]
    public void UnNombreDeUsuarioDemasiadoLargoSeRechaza()
    {
        var largo = new string('a', ValidadorEntrada.LongitudMaximaNombreUsuario + 1);

        Assert.Throws<ExcepcionValidacion>(() => ValidadorEntrada.ValidarNombreUsuario(largo));
    }

    [Fact]
    public void ElCorreoSeNormalizaAMinusculas()
        => Assert.Equal("ana@dotchat.local", ValidadorEntrada.ValidarEmail("  Ana@DotChat.Local  "));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ana")]
    [InlineData("ana@")]
    [InlineData("ana@local")]
    [InlineData("ana@@dotchat.local")]
    [InlineData("ana eva@dotchat.local")]
    public void UnCorreoMalFormadoSeRechaza(string? entrada)
        => Assert.Throws<ExcepcionValidacion>(() => ValidadorEntrada.ValidarEmail(entrada));

    [Fact]
    public void UnaClaveValidaSeDevuelveTalCual()
    {
        // No se recorta: los espacios de una contraseña pueden ser intencionados.
        const string clave = "  clave larga y buena  ";

        Assert.Equal(clave, ValidadorEntrada.ValidarClave(clave));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("corta")]
    public void UnaClaveDemasiadoCortaOAusenteSeRechaza(string? entrada)
        => Assert.Throws<ExcepcionValidacion>(() => ValidadorEntrada.ValidarClave(entrada));

    [Fact]
    public void UnaClaveDesmesuradaSeRechaza()
    {
        // El tope existe para que nadie pueda ahogar al servidor obligándole a calcular
        // el hash de una contraseña de megabytes.
        var enorme = new string('a', ValidadorEntrada.LongitudMaximaClave + 1);

        Assert.Throws<ExcepcionValidacion>(() => ValidadorEntrada.ValidarClave(enorme));
    }

    [Fact]
    public void UnaClaveConCaracteresDeControlSeRechaza()
        => Assert.Throws<ExcepcionValidacion>(() => ValidadorEntrada.ValidarClave("clave\u0007larga"));

    [Theory]
    [InlineData("General", "General")]
    [InlineData("  Sala de   pruebas  ", "Sala de pruebas")]
    [InlineData("equipo-1_a", "equipo-1_a")]
    public void UnNombreDeSalaBienFormadoSeSanea(string entrada, string esperado)
        => Assert.Equal(esperado, ValidadorEntrada.ValidarNombreSala(entrada));

    [Theory]
    [InlineData(null)]
    [InlineData("ab")]
    [InlineData("-sala")]
    [InlineData("sala-")]
    [InlineData("_sala_")]
    [InlineData("sala!")]
    public void UnNombreDeSalaMalFormadoSeRechaza(string? entrada)
        => Assert.Throws<ExcepcionValidacion>(() => ValidadorEntrada.ValidarNombreSala(entrada));

    [Fact]
    public void UnNombreDeSalaDemasiadoLargoSeRechaza()
    {
        var largo = new string('a', ValidadorEntrada.LongitudMaximaNombreSala + 1);

        Assert.Throws<ExcepcionValidacion>(() => ValidadorEntrada.ValidarNombreSala(largo));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void UnaDescripcionVaciaSeGuardaComoAusente(string? entrada)
        => Assert.Null(ValidadorEntrada.ValidarDescripcionSala(entrada));

    [Fact]
    public void UnaDescripcionSeSaneaYSeLimita()
    {
        Assert.Equal("Sala de pruebas", ValidadorEntrada.ValidarDescripcionSala("  Sala   de pruebas "));

        Assert.Throws<ExcepcionValidacion>(() => ValidadorEntrada.ValidarDescripcionSala(
            new string('a', ValidadorEntrada.LongitudMaximaDescripcionSala + 1)));
    }

    [Fact]
    public void UnMensajeDemasiadoLargoSeRechaza()
        => Assert.Throws<ExcepcionValidacion>(() => ValidadorEntrada.ValidarTextoMensaje(new string('a', 11), 10));

    [Fact]
    public void UnMensajeQueOcupaExactamenteElLimiteSeAcepta()
    {
        var justo = new string('a', 10);

        Assert.Equal(justo, ValidadorEntrada.ValidarTextoMensaje(justo, 10));
    }

    [Fact]
    public void ElIdentificadorVacioSeRechazaConElNombreDelCampo()
    {
        var excepcion = Assert.Throws<ExcepcionValidacion>(
            () => ValidadorEntrada.ValidarIdentificador(Guid.Empty, "salaId"));

        Assert.True(excepcion.Errores.ContainsKey("salaId"));
    }

    [Fact]
    public void UnIdentificadorValidoSeDevuelveIntacto()
    {
        var id = Guid.CreateVersion7();

        Assert.Equal(id, ValidadorEntrada.ValidarIdentificador(id, "salaId"));
    }

    [Theory]
    [InlineData(0, 50)]
    [InlineData(-5, 50)]
    [InlineData(10, 10)]
    [InlineData(200, 200)]
    [InlineData(5000, 200)]
    public void LaCantidadSeAjustaAlRangoPermitido(int solicitada, int esperada)
        => Assert.Equal(esperada, ValidadorEntrada.NormalizarCantidad(solicitada));

    [Fact]
    public void SanearDevuelveVacioParaLoQueNoTieneContenido()
    {
        Assert.Equal(string.Empty, ValidadorEntrada.Sanear(null));
        Assert.Equal(string.Empty, ValidadorEntrada.Sanear("   "));
    }

    [Fact]
    public void SanearColapsaLosEspaciosYRecortaLosExtremos()
        => Assert.Equal("hola que tal", ValidadorEntrada.Sanear("  hola   que \t tal \n "));

    [Fact]
    public void SanearNormalizaAFormaCanonica()
    {
        // «e» más acento combinante y «é» precompuesta deben acabar siendo lo mismo:
        // si no, dos nombres visualmente idénticos serían dos usuarios distintos.
        Assert.Equal("\u00E9", ValidadorEntrada.Sanear("e\u0301"));
        Assert.Equal(1, ValidadorEntrada.Sanear("e\u0301").Length);
    }

    [Fact]
    public void SanearDescartaLasSecuenciasDeEscapeDeConsola()
    {
        // Un mensaje con secuencias ANSI podría repintar la terminal de quien lo lee.
        var resultado = ValidadorEntrada.Sanear("hola\u001B[31mrojo");

        Assert.DoesNotContain('\u001B', resultado);
        Assert.Equal("hola [31mrojo", resultado);
    }
}
