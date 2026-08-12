using Chat.Dominio.Entidades;
using Chat.Tests.Comun;

namespace Chat.Tests.Dominio;

/// <summary>
/// Pruebas de las reglas que las entidades resuelven por sí solas: las claves
/// canónicas y el ciclo de vida de un token de refresco.
/// </summary>
public sealed class PruebasEntidades
{
    [Fact]
    public void LaClaveDeUnaConversacionDirectaNoDependeDelOrdenDeLosParticipantes()
    {
        // Es lo que impide que Ana y Eva acaben con dos conversaciones distintas según
        // quién la abriera primero.
        var ana = Guid.CreateVersion7();
        var eva = Guid.CreateVersion7();

        Assert.Equal(
            Sala.ConstruirClaveDirecta(ana, eva),
            Sala.ConstruirClaveDirecta(eva, ana));
    }

    [Fact]
    public void DosParejasDistintasNoCompartenClave()
    {
        var ana = Guid.CreateVersion7();
        var eva = Guid.CreateVersion7();
        var leo = Guid.CreateVersion7();

        Assert.NotEqual(
            Sala.ConstruirClaveDirecta(ana, eva),
            Sala.ConstruirClaveDirecta(ana, leo));
    }

    [Fact]
    public void LaClaveDirectaOrdenaLosIdentificadoresYLosSepara()
    {
        var menor = new Guid("00000000-0000-0000-0000-000000000001");
        var mayor = new Guid("ffffffff-ffff-ffff-ffff-ffffffffffff");

        Assert.Equal($"{menor:N}:{mayor:N}", Sala.ConstruirClaveDirecta(mayor, menor));
    }

    [Fact]
    public void ElNombreInternoDeUnaDirectaLlevaSuPrefijoYNoColisionaConUnaSalaNormal()
    {
        var clave = Sala.ConstruirClaveDirecta(Guid.CreateVersion7(), Guid.CreateVersion7());

        var nombre = Sala.ConstruirNombreDirecto(clave);

        Assert.StartsWith("directa:", nombre, StringComparison.Ordinal);
        Assert.Contains(clave, nombre, StringComparison.Ordinal);
    }

    [Fact]
    public void LaClaveDeUnAdjuntoSeRepartePorSalaYPorMes()
    {
        // Sin este reparto, el almacén acabaría con todos los objetos colgando de la
        // raíz, lo que degrada el listado y el borrado por lotes.
        var salaId = Guid.CreateVersion7();
        var adjuntoId = Guid.CreateVersion7();

        var clave = Adjunto.ConstruirClave(salaId, adjuntoId, new DateTimeOffset(2026, 3, 9, 0, 0, 0, TimeSpan.Zero));

        Assert.Equal($"salas/{salaId:N}/2026/03/{adjuntoId:N}", clave);
    }

    [Fact]
    public void LaClaveDeUnAdjuntoNoDependeDeLaZonaHorariaDeQuienLaConstruye()
    {
        var salaId = Guid.CreateVersion7();
        var adjuntoId = Guid.CreateVersion7();
        var fecha = new DateTimeOffset(2026, 12, 1, 3, 0, 0, TimeSpan.FromHours(5));

        Assert.Contains("/2026/12/", Adjunto.ConstruirClave(salaId, adjuntoId, fecha), StringComparison.Ordinal);
    }

    [Fact]
    public void UnAdjuntoDeImagenSeAnunciaComoDibujable()
    {
        var salaId = Guid.CreateVersion7();

        Assert.True(Datos.Adjunto(salaId, Guid.CreateVersion7()).EsImagen);
        Assert.False(Datos.Adjunto(salaId, Guid.CreateVersion7(), tipo: TipoAdjunto.Archivo).EsImagen);
    }

    [Fact]
    public void UnTokenReciénEmitidoEsValido()
    {
        var token = Datos.TokenRefresco(Guid.CreateVersion7(), expiracion: Datos.Ahora.AddDays(7));

        Assert.True(token.EsValido(Datos.Ahora));
        Assert.False(token.EstaRevocado);
    }

    [Fact]
    public void UnTokenCaducadoDejaDeSerValido()
    {
        var token = Datos.TokenRefresco(Guid.CreateVersion7(), expiracion: Datos.Ahora);

        // La comparación es estricta: en el instante exacto de expiración ya no vale.
        Assert.False(token.EsValido(Datos.Ahora));
        Assert.False(token.EsValido(Datos.Ahora.AddSeconds(1)));
        Assert.True(token.EsValido(Datos.Ahora.AddSeconds(-1)));
    }

    [Fact]
    public void UnTokenRevocadoDejaDeSerValidoAunqueNoHayaCaducado()
    {
        var token = Datos.TokenRefresco(Guid.CreateVersion7(), expiracion: Datos.Ahora.AddDays(7));

        token.Revocar(Datos.Ahora);

        Assert.True(token.EstaRevocado);
        Assert.False(token.EsValido(Datos.Ahora));
    }

    [Fact]
    public void RevocarDosVecesConservaLaFechaDeLaPrimeraRevocacion()
    {
        // Importa para la auditoría: la fecha que interesa es la del primer uso, que es
        // cuando la sesión dejó de ser legítima.
        var token = Datos.TokenRefresco(Guid.CreateVersion7());

        token.Revocar(Datos.Ahora);
        token.Revocar(Datos.Ahora.AddHours(1));

        Assert.Equal(Datos.Ahora, token.FechaRevocacion);
    }

    [Fact]
    public void UnUsuarioNaceActivoYSinHistorial()
    {
        var usuario = new Usuario();

        Assert.True(usuario.Activo);
        Assert.Null(usuario.FechaUltimoAcceso);
        Assert.Empty(usuario.Mensajes);
        Assert.Empty(usuario.Membresias);
        Assert.Empty(usuario.TokensRefresco);
    }

    [Fact]
    public void UnaSalaNaceComoPublicaYSinActividad()
    {
        var sala = new Sala { Nombre = "General" };

        Assert.Equal(TipoSala.Publica, sala.Tipo);
        Assert.Null(sala.FechaUltimaActividad);
        Assert.Null(sala.ClaveDirecta);
    }

    [Fact]
    public void LosIdentificadoresSeGeneranOrdenablesPorTiempo()
    {
        // La versión 7 codifica la marca de tiempo en los bits altos: dos entidades
        // creadas en orden mantienen ese orden al compararlas como cadena, que es de lo
        // que dependen los índices y la paginación del historial.
        var primero = new Mensaje { SalaId = Guid.Empty, UsuarioId = Guid.Empty }.Id;
        Thread.Sleep(2);
        var segundo = new Mensaje { SalaId = Guid.Empty, UsuarioId = Guid.Empty }.Id;

        Assert.Equal(7, primero.Version);
        Assert.True(string.CompareOrdinal(primero.ToString("D"), segundo.ToString("D")) < 0);
    }
}
