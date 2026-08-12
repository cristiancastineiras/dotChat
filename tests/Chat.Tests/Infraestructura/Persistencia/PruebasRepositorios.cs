using Chat.Dominio.Entidades;
using Chat.Tests.Comun;
using Microsoft.EntityFrameworkCore;

namespace Chat.Tests.Infraestructura.Persistencia;

/// <summary>Pruebas del repositorio de usuarios.</summary>
public sealed class PruebasRepositorioUsuarios : IDisposable
{
    private readonly BaseDatosDePrueba _bd = new();

    /// <inheritdoc />
    public void Dispose() => _bd.Dispose();

    [Fact]
    public async Task UnUsuarioSeRecuperaPorSuIdentificador()
    {
        var ana = await _bd.SembrarUsuarioAsync("ana");
        _bd.Olvidar();

        Assert.Equal("ana", (await _bd.Usuarios.ObtenerPorIdAsync(ana.Id))!.UserName);
    }

    [Fact]
    public async Task UnIdentificadorDesconocidoDevuelveNulo()
        => Assert.Null(await _bd.Usuarios.ObtenerPorIdAsync(Guid.CreateVersion7()));

    [Theory]
    [InlineData("ana")]
    [InlineData("ANA")]
    [InlineData("AnA")]
    public async Task LaBusquedaPorNombreUsaElCampoNormalizadoDeIdentity(string buscado)
    {
        await _bd.SembrarUsuarioAsync("ana");
        _bd.Olvidar();

        Assert.NotNull(await _bd.Usuarios.ObtenerPorNombreAsync(buscado));
    }

    [Fact]
    public async Task UnNombreDesconocidoDevuelveNulo()
        => Assert.Null(await _bd.Usuarios.ObtenerPorNombreAsync("nadie"));

    [Fact]
    public async Task ElListadoPorDefectoOcultaLasCuentasDesactivadas()
    {
        await _bd.SembrarUsuarioAsync("ana");
        await _bd.SembrarUsuarioAsync("leo", activo: false);
        _bd.Olvidar();

        Assert.Equal(["ana"], (await _bd.Usuarios.ListarAsync(incluirInactivos: false)).Select(u => u.UserName));
    }

    [Fact]
    public async Task ElListadoCompletoIncluyeLasDesactivadasYVaOrdenadoPorNombre()
    {
        await _bd.SembrarUsuarioAsync("zeta");
        await _bd.SembrarUsuarioAsync("alfa", activo: false);
        _bd.Olvidar();

        Assert.Equal(["alfa", "zeta"], (await _bd.Usuarios.ListarAsync(incluirInactivos: true)).Select(u => u.UserName));
    }

    [Fact]
    public async Task ElRecuentoIncluyeTodasLasCuentas()
    {
        await _bd.SembrarUsuarioAsync("ana");
        await _bd.SembrarUsuarioAsync("leo", activo: false);

        Assert.Equal(2, await _bd.Usuarios.ContarAsync());
    }

    [Fact]
    public async Task EliminarUnaCuentaLaBorraDeVerdad()
    {
        var ana = await _bd.SembrarUsuarioAsync("ana");

        _bd.Usuarios.Eliminar((await _bd.Usuarios.ObtenerPorIdAsync(ana.Id))!);
        await _bd.UnidadDeTrabajo.GuardarCambiosAsync();
        _bd.Olvidar();

        Assert.Null(await _bd.Usuarios.ObtenerPorIdAsync(ana.Id));
    }
}

/// <summary>Pruebas del repositorio de mensajes.</summary>
public sealed class PruebasRepositorioMensajes : IDisposable
{
    private readonly BaseDatosDePrueba _bd = new();

    /// <inheritdoc />
    public void Dispose() => _bd.Dispose();

    [Fact]
    public async Task ElHistorialDevuelveLosMasRecientesEnOrdenDeLectura()
    {
        // Se toman los N últimos con el índice descendente y se devuelven en orden
        // cronológico, que es como se pintan en pantalla.
        var (sala, ana) = await PrepararAsync();

        for (var i = 0; i < 5; i++)
        {
            await _bd.SembrarAsync(Datos.Mensaje(sala.Id, ana.Id, $"m{i}", Datos.Ahora.AddMinutes(i)));
        }

        _bd.Olvidar();

        var pagina = await _bd.Mensajes.ObtenerRecientesAsync(sala.Id, 3, null);

        Assert.Equal(["m2", "m3", "m4"], pagina.Select(m => m.TextoCifrado));
    }

    [Fact]
    public async Task ElHistorialTraeElAutorYElAdjuntoCargados()
    {
        var (sala, ana) = await PrepararAsync();
        var adjunto = Datos.Adjunto(sala.Id, ana.Id);
        await _bd.SembrarAsync(adjunto);
        await _bd.SembrarAsync(Datos.Mensaje(sala.Id, ana.Id, adjuntoId: adjunto.Id));
        _bd.Olvidar();

        var mensaje = Assert.Single(await _bd.Mensajes.ObtenerRecientesAsync(sala.Id, 10, null));

        Assert.Equal("ana", mensaje.Usuario!.UserName);
        Assert.Equal("foto.png", mensaje.Adjunto!.NombreArchivo);
    }

    [Fact]
    public async Task LaPaginacionHaciaAtrasDevuelveSoloLoAnterior()
    {
        var (sala, ana) = await PrepararAsync();

        for (var i = 0; i < 5; i++)
        {
            await _bd.SembrarAsync(Datos.Mensaje(sala.Id, ana.Id, $"m{i}", Datos.Ahora.AddMinutes(i)));
        }

        _bd.Olvidar();

        var pagina = await _bd.Mensajes.ObtenerRecientesAsync(sala.Id, 10, Datos.Ahora.AddMinutes(2));

        Assert.Equal(["m0", "m1"], pagina.Select(m => m.TextoCifrado));
    }

    [Fact]
    public async Task ElHistorialNoMezclaSalas()
    {
        var (sala, ana) = await PrepararAsync();
        var otra = Datos.Sala(nombre: "Otra");
        await _bd.SembrarAsync(otra);
        await _bd.SembrarAsync(
            Datos.Mensaje(sala.Id, ana.Id, "propio"),
            Datos.Mensaje(otra.Id, ana.Id, "ajeno"));
        _bd.Olvidar();

        var pagina = await _bd.Mensajes.ObtenerRecientesAsync(sala.Id, 10, null);

        Assert.Equal(["propio"], pagina.Select(m => m.TextoCifrado));
    }

    [Fact]
    public async Task LosMensajesSeCuentanEnTotalYPorSala()
    {
        var (sala, ana) = await PrepararAsync();
        var otra = Datos.Sala(nombre: "Otra");
        await _bd.SembrarAsync(otra);
        await _bd.SembrarAsync(
            Datos.Mensaje(sala.Id, ana.Id),
            Datos.Mensaje(sala.Id, ana.Id),
            Datos.Mensaje(otra.Id, ana.Id));

        Assert.Equal(3, await _bd.Mensajes.ContarAsync(null));
        Assert.Equal(2, await _bd.Mensajes.ContarAsync(sala.Id));
    }

    [Fact]
    public async Task ElUltimoMensajeDeCadaSalaSeResuelveEnUnaSolaConsulta()
    {
        var (primera, ana) = await PrepararAsync();
        var segunda = Datos.Sala(nombre: "Segunda");
        var vacia = Datos.Sala(nombre: "Vacia");
        await _bd.SembrarAsync(segunda, vacia);

        await _bd.SembrarAsync(
            Datos.Mensaje(primera.Id, ana.Id, "viejo", Datos.Ahora),
            Datos.Mensaje(primera.Id, ana.Id, "nuevo", Datos.Ahora.AddMinutes(5)),
            Datos.Mensaje(segunda.Id, ana.Id, "unico", Datos.Ahora));
        _bd.Olvidar();

        var ultimos = await _bd.Mensajes.ObtenerUltimosPorSalaAsync([primera.Id, segunda.Id, vacia.Id]);

        Assert.Equal(2, ultimos.Count);
        Assert.Equal("nuevo", ultimos[primera.Id].TextoCifrado);
        Assert.Equal("ana", ultimos[primera.Id].NombreAutor);
        Assert.Equal("unico", ultimos[segunda.Id].TextoCifrado);

        // Una sala sin mensajes no aparece: no hay nada que previsualizar.
        Assert.False(ultimos.ContainsKey(vacia.Id));
    }

    [Fact]
    public async Task ElUltimoMensajeLlevaLaFichaDelAdjuntoSiLoTenia()
    {
        var (sala, ana) = await PrepararAsync();
        var adjunto = Datos.Adjunto(sala.Id, ana.Id);
        await _bd.SembrarAsync(adjunto);
        await _bd.SembrarAsync(Datos.Mensaje(sala.Id, ana.Id, textoCifrado: null, adjuntoId: adjunto.Id));
        _bd.Olvidar();

        var ultimo = (await _bd.Mensajes.ObtenerUltimosPorSalaAsync([sala.Id]))[sala.Id];

        Assert.Null(ultimo.TextoCifrado);
        Assert.Equal("foto.png", ultimo.NombreAdjunto);
        Assert.Equal(TipoAdjunto.Imagen, ultimo.TipoAdjunto);
    }

    [Fact]
    public async Task PedirLosUltimosDeUnConjuntoVacioNoConsultaNada()
        => Assert.Empty(await _bd.Mensajes.ObtenerUltimosPorSalaAsync([]));

    [Fact]
    public async Task UnConjuntoDeSalasNuloSeRechaza()
        => await Assert.ThrowsAsync<ArgumentNullException>(() => _bd.Mensajes.ObtenerUltimosPorSalaAsync(null!));

    [Fact]
    public async Task LosPendientesCuentanSoloLoAjenoYPosteriorALaLectura()
    {
        var (sala, ana) = await PrepararAsync();
        var eva = await _bd.SembrarUsuarioAsync("eva");

        // Ana leyó la sala hace una hora.
        await _bd.SembrarAsync(Datos.Membresia(sala.Id, ana.Id, ultimaLectura: Datos.Ahora));

        await _bd.SembrarAsync(
            Datos.Mensaje(sala.Id, eva.Id, "antes", Datos.Ahora.AddMinutes(-5)),
            Datos.Mensaje(sala.Id, eva.Id, "despues1", Datos.Ahora.AddMinutes(5)),
            Datos.Mensaje(sala.Id, eva.Id, "despues2", Datos.Ahora.AddMinutes(6)),
            Datos.Mensaje(sala.Id, ana.Id, "propio", Datos.Ahora.AddMinutes(7)));
        _bd.Olvidar();

        var pendientes = await _bd.Mensajes.ContarNoLeidosPorSalaAsync(ana.Id);

        Assert.Equal(2, pendientes[sala.Id]);
    }

    [Fact]
    public async Task SinMarcaDeLecturaCuentaTodoLoAjeno()
    {
        // Una marca nula significa que el usuario no ha abierto la sala desde que entró.
        var (sala, ana) = await PrepararAsync();
        var eva = await _bd.SembrarUsuarioAsync("eva");

        await _bd.SembrarAsync(Datos.Membresia(sala.Id, ana.Id));
        await _bd.SembrarAsync(
            Datos.Mensaje(sala.Id, eva.Id, "uno"),
            Datos.Mensaje(sala.Id, eva.Id, "dos"));
        _bd.Olvidar();

        Assert.Equal(2, (await _bd.Mensajes.ContarNoLeidosPorSalaAsync(ana.Id))[sala.Id]);
    }

    [Fact]
    public async Task UnaSalaAlDiaNoApareceEntreLosPendientes()
    {
        var (sala, ana) = await PrepararAsync();
        await _bd.SembrarAsync(Datos.Membresia(sala.Id, ana.Id, ultimaLectura: Datos.Ahora.AddHours(1)));
        await _bd.SembrarAsync(Datos.Mensaje(sala.Id, ana.Id));
        _bd.Olvidar();

        Assert.Empty(await _bd.Mensajes.ContarNoLeidosPorSalaAsync(ana.Id));
    }

    /// <summary>Deja una sala y un usuario sembrados.</summary>
    private async Task<(Sala Sala, Usuario Ana)> PrepararAsync()
    {
        var ana = await _bd.SembrarUsuarioAsync("ana");
        var sala = Datos.Sala();
        await _bd.SembrarAsync(sala);

        return (sala, ana);
    }
}

/// <summary>Pruebas del repositorio de adjuntos, incluida la purga de huérfanos.</summary>
public sealed class PruebasRepositorioAdjuntos : IDisposable
{
    private readonly BaseDatosDePrueba _bd = new();

    /// <inheritdoc />
    public void Dispose() => _bd.Dispose();

    [Fact]
    public async Task UnAdjuntoSeGuardaYSeRecupera()
    {
        var (sala, ana) = await PrepararAsync();
        var adjunto = Datos.Adjunto(sala.Id, ana.Id);

        await _bd.Adjuntos.AgregarAsync(adjunto);
        await _bd.UnidadDeTrabajo.GuardarCambiosAsync();
        _bd.Olvidar();

        var recuperado = await _bd.Adjuntos.ObtenerPorIdAsync(adjunto.Id);

        Assert.NotNull(recuperado);
        Assert.Equal("foto.png", recuperado.NombreArchivo);
        Assert.Equal(640, recuperado.Ancho);
    }

    [Fact]
    public async Task UnIdentificadorDesconocidoDevuelveNulo()
        => Assert.Null(await _bd.Adjuntos.ObtenerPorIdAsync(Guid.CreateVersion7()));

    [Fact]
    public async Task UnAdjuntoSoloConstaComoPublicadoCuandoLoUsaUnMensaje()
    {
        var (sala, ana) = await PrepararAsync();
        var adjunto = Datos.Adjunto(sala.Id, ana.Id);
        await _bd.SembrarAsync(adjunto);

        Assert.False(await _bd.Adjuntos.EstaPublicadoAsync(adjunto.Id));

        await _bd.SembrarAsync(Datos.Mensaje(sala.Id, ana.Id, adjuntoId: adjunto.Id));

        Assert.True(await _bd.Adjuntos.EstaPublicadoAsync(adjunto.Id));
    }

    [Fact]
    public async Task SeCuentanLosAdjuntosYSeSumaSuTamano()
    {
        var (sala, ana) = await PrepararAsync();

        Assert.Equal(0, await _bd.Adjuntos.ContarAsync());
        Assert.Equal(0L, await _bd.Adjuntos.SumarTamanoAsync());

        await _bd.SembrarAsync(Datos.Adjunto(sala.Id, ana.Id), Datos.Adjunto(sala.Id, ana.Id));

        Assert.Equal(2, await _bd.Adjuntos.ContarAsync());
        Assert.Equal(2048L, await _bd.Adjuntos.SumarTamanoAsync());
    }

    [Fact]
    public async Task LaPurgaSeLlevaLosHuerfanosAntiguosYDevuelveSusClaves()
    {
        // Los adjuntos que nunca llegaron a publicarse ocuparían sitio para siempre.
        var (sala, ana) = await PrepararAsync();

        var huerfano = Datos.Adjunto(sala.Id, ana.Id, fechaCreacion: Datos.Ahora.AddHours(-5));
        var reciente = Datos.Adjunto(sala.Id, ana.Id, fechaCreacion: Datos.Ahora);
        var publicado = Datos.Adjunto(sala.Id, ana.Id, fechaCreacion: Datos.Ahora.AddHours(-5));

        await _bd.SembrarAsync(huerfano, reciente, publicado);
        await _bd.SembrarAsync(Datos.Mensaje(sala.Id, ana.Id, adjuntoId: publicado.Id));
        _bd.Olvidar();

        var claves = await _bd.Adjuntos.PurgarHuerfanosAsync(Datos.Ahora.AddHours(-1));

        Assert.Equal([huerfano.ClaveObjeto], claves);

        using var comprobacion = _bd.CrearContexto();
        Assert.Equal(2, await comprobacion.Adjuntos.CountAsync());
        Assert.False(await comprobacion.Adjuntos.AnyAsync(a => a.Id == huerfano.Id));
    }

    [Fact]
    public async Task SinHuerfanosLaPurgaNoDevuelveNada()
    {
        var (sala, ana) = await PrepararAsync();
        await _bd.SembrarAsync(Datos.Adjunto(sala.Id, ana.Id, fechaCreacion: Datos.Ahora));
        _bd.Olvidar();

        Assert.Empty(await _bd.Adjuntos.PurgarHuerfanosAsync(Datos.Ahora.AddHours(-1)));
        Assert.Equal(1, await _bd.Adjuntos.ContarAsync());
    }

    /// <summary>Deja una sala y un usuario sembrados.</summary>
    private async Task<(Sala Sala, Usuario Ana)> PrepararAsync()
    {
        var ana = await _bd.SembrarUsuarioAsync("ana");
        var sala = Datos.Sala();
        await _bd.SembrarAsync(sala);

        return (sala, ana);
    }
}

/// <summary>Pruebas del repositorio de tokens de refresco.</summary>
public sealed class PruebasRepositorioTokensRefresco : IDisposable
{
    private readonly BaseDatosDePrueba _bd = new();

    /// <inheritdoc />
    public void Dispose() => _bd.Dispose();

    [Fact]
    public async Task UnTokenSeGuardaYSeBuscaPorSuHash()
    {
        var ana = await _bd.SembrarUsuarioAsync("ana");

        await _bd.TokensRefresco.AgregarAsync(Datos.TokenRefresco(ana.Id, "hash-abc"));
        await _bd.UnidadDeTrabajo.GuardarCambiosAsync();
        _bd.Olvidar();

        var token = await _bd.TokensRefresco.ObtenerPorHashAsync("hash-abc");

        Assert.NotNull(token);
        Assert.Equal(ana.Id, token.UsuarioId);
        Assert.False(token.EstaRevocado);
    }

    [Fact]
    public async Task UnHashDesconocidoDevuelveNulo()
        => Assert.Null(await _bd.TokensRefresco.ObtenerPorHashAsync("no-existe"));

    [Fact]
    public async Task RevocarTodosCierraSoloLasSesionesActivasDeEseUsuario()
    {
        var ana = await _bd.SembrarUsuarioAsync("ana");
        var eva = await _bd.SembrarUsuarioAsync("eva");

        var deAna = Datos.TokenRefresco(ana.Id, "a1");
        var deAnaYaRevocado = Datos.TokenRefresco(ana.Id, "a2", revocacion: Datos.Ahora.AddDays(-1));
        var deEva = Datos.TokenRefresco(eva.Id, "e1");

        await _bd.SembrarAsync(deAna, deAnaYaRevocado, deEva);
        _bd.Olvidar();

        await _bd.TokensRefresco.RevocarTodosAsync(ana.Id, Datos.Ahora);
        await _bd.UnidadDeTrabajo.GuardarCambiosAsync();
        _bd.Olvidar();

        Assert.Equal(Datos.Ahora, (await _bd.TokensRefresco.ObtenerPorHashAsync("a1"))!.FechaRevocacion);
        Assert.Equal(Datos.Ahora.AddDays(-1), (await _bd.TokensRefresco.ObtenerPorHashAsync("a2"))!.FechaRevocacion);
        Assert.False((await _bd.TokensRefresco.ObtenerPorHashAsync("e1"))!.EstaRevocado);
    }

    [Fact]
    public async Task LaPurgaSeLlevaLosCaducadosYLosRevocadosPeroNoLosVigentes()
    {
        var ana = await _bd.SembrarUsuarioAsync("ana");

        var vigente = Datos.TokenRefresco(ana.Id, "vigente", Datos.Ahora.AddDays(7));
        var caducado = Datos.TokenRefresco(ana.Id, "caducado", Datos.Ahora.AddDays(-1));
        var revocado = Datos.TokenRefresco(ana.Id, "revocado", Datos.Ahora.AddDays(7), Datos.Ahora);

        await _bd.SembrarAsync(vigente, caducado, revocado);
        _bd.Olvidar();

        var eliminados = await _bd.TokensRefresco.PurgarAsync(Datos.Ahora);

        Assert.Equal(2, eliminados);
        Assert.NotNull(await _bd.TokensRefresco.ObtenerPorHashAsync("vigente"));
        Assert.Null(await _bd.TokensRefresco.ObtenerPorHashAsync("caducado"));
        Assert.Null(await _bd.TokensRefresco.ObtenerPorHashAsync("revocado"));
    }

    [Fact]
    public async Task SinNadaQuePurgarLaOperacionNoBorraNada()
    {
        var ana = await _bd.SembrarUsuarioAsync("ana");
        await _bd.SembrarAsync(Datos.TokenRefresco(ana.Id, "vigente", Datos.Ahora.AddDays(7)));
        _bd.Olvidar();

        Assert.Equal(0, await _bd.TokensRefresco.PurgarAsync(Datos.Ahora));
    }
}
