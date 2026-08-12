using Chat.Aplicacion.Abstracciones;
using Chat.Aplicacion.Consultas.Salas;
using Chat.Dominio.Abstracciones;
using Chat.Dominio.Entidades;
using Chat.Dominio.Excepciones;
using Chat.Tests.Comun;
using NSubstitute;

namespace Chat.Tests.Aplicacion.Consultas;

/// <summary>Pruebas del catálogo de salas y de su filtro de visibilidad.</summary>
public sealed class PruebasListarSalas
{
    private readonly IRepositorioSalas _salas = Substitute.For<IRepositorioSalas>();
    private readonly CacheDePrueba _cache = new();

    private readonly Sala _publica = Datos.Sala(nombre: "Publica");
    private readonly Sala _privadaPropia = Datos.Sala(nombre: "PrivadaPropia", tipo: TipoSala.Privada);
    private readonly Sala _privadaAjena = Datos.Sala(nombre: "PrivadaAjena", tipo: TipoSala.Privada);
    private readonly Sala _directa = Datos.Sala(nombre: "directa:x:y", tipo: TipoSala.Directa, claveDirecta: "x:y");

    private readonly Guid _usuarioId = Guid.CreateVersion7();

    public PruebasListarSalas()
    {
        _salas
            .ListarAsync(Arg.Any<CancellationToken>())
            .Returns([_publica, _privadaPropia, _privadaAjena, _directa]);

        _salas
            .ListarSalasDeUsuarioAsync(_usuarioId, Arg.Any<CancellationToken>())
            .Returns([_privadaPropia.Id]);
    }

    private ManejadorListarSalas Manejador => new(_salas, _cache, Opciones.De(Opciones.Cache()));

    [Fact]
    public async Task ElCatalogoMuestraLasPublicasYLasPrivadasPropias()
    {
        var resultado = await Manejador.ManejarAsync(new ConsultaListarSalas(_usuarioId));

        Assert.Equal(["Publica", "PrivadaPropia"], resultado.Select(s => s.Nombre));
    }

    [Fact]
    public async Task LasConversacionesDirectasNuncaAparecenEnElCatalogo()
    {
        var resultado = await Manejador.ManejarAsync(new ConsultaListarSalas(_usuarioId));

        Assert.DoesNotContain(resultado, sala => sala.Tipo == TipoSala.Directa);
    }

    [Fact]
    public async Task LaPertenenciaSeMarcaParaQuienConsulta()
    {
        var resultado = await Manejador.ManejarAsync(new ConsultaListarSalas(_usuarioId));

        Assert.False(resultado.Single(s => s.Nombre == "Publica").EsMiembro);
        Assert.True(resultado.Single(s => s.Nombre == "PrivadaPropia").EsMiembro);
    }

    [Fact]
    public async Task LaAuditoriaAdministrativaLoVeTodo()
    {
        var resultado = await Manejador.ManejarAsync(new ConsultaListarSalas(_usuarioId, IncluirTodas: true));

        Assert.Equal(4, resultado.Count);
    }

    [Fact]
    public async Task SinUsuarioSoloSeVenLasSalasPublicas()
    {
        var resultado = await Manejador.ManejarAsync(new ConsultaListarSalas(Guid.Empty));

        Assert.Equal(["Publica"], resultado.Select(s => s.Nombre));
        await _salas.DidNotReceiveWithAnyArgs().ListarSalasDeUsuarioAsync(default);
    }

    [Fact]
    public async Task ElCatalogoSeCacheaPeroLaPertenenciaSeResuelveEnCadaConsulta()
    {
        // La pertenencia depende de quién pregunte, así que no puede quedar congelada
        // dentro de la entrada cacheada.
        var otro = Guid.CreateVersion7();
        _salas.ListarSalasDeUsuarioAsync(otro, Arg.Any<CancellationToken>()).Returns([_publica.Id]);

        var primera = await Manejador.ManejarAsync(new ConsultaListarSalas(_usuarioId));
        var segunda = await Manejador.ManejarAsync(new ConsultaListarSalas(otro));

        Assert.Equal(1, _cache.Generaciones);
        await _salas.Received(1).ListarAsync(Arg.Any<CancellationToken>());

        Assert.True(primera.Single(s => s.Nombre == "PrivadaPropia").EsMiembro);
        Assert.True(segunda.Single(s => s.Nombre == "Publica").EsMiembro);
    }

    [Fact]
    public async Task ElCatalogoCacheadoLlevaElRecuentoDeMiembros()
    {
        _privadaPropia.Miembros.Add(Datos.Membresia(_privadaPropia.Id, _usuarioId));

        var resultado = await Manejador.ManejarAsync(new ConsultaListarSalas(_usuarioId));

        Assert.Equal(1, resultado.Single(s => s.Nombre == "PrivadaPropia").TotalMiembros);
        Assert.Equal(0, resultado.Single(s => s.Nombre == "Publica").TotalMiembros);
    }

    [Fact]
    public async Task UnaConsultaNulaSeRechaza()
        => await Assert.ThrowsAsync<ArgumentNullException>(() => Manejador.ManejarAsync(null!));
}

/// <summary>Pruebas de la bandeja de conversaciones de un usuario.</summary>
public sealed class PruebasSalasDeUsuario
{
    private readonly IRepositorioSalas _salas = Substitute.For<IRepositorioSalas>();
    private readonly IRepositorioMensajes _mensajes = Substitute.For<IRepositorioMensajes>();
    private readonly ICifradorMensajes _cifrador = Substitute.For<ICifradorMensajes>();
    private readonly IRegistroConexiones _conexiones = Substitute.For<IRegistroConexiones>();

    private readonly Usuario _ana = Datos.Usuario(nombre: "ana");
    private readonly Usuario _eva = Datos.Usuario(nombre: "eva");

    public PruebasSalasDeUsuario()
    {
        _cifrador
            .IntentarDescifrar(Arg.Any<string>(), out Arg.Any<string?>())
            .Returns(llamada =>
            {
                llamada[1] = $"claro({(string)llamada[0]!})";
                return true;
            });

        _conexiones
            .FiltrarConectadosAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns<IReadOnlySet<Guid>>(_ => new HashSet<Guid>());
    }

    private ManejadorSalasDeUsuario Manejador => new(_salas, _mensajes, _cifrador, _conexiones);

    [Fact]
    public async Task SinConversacionesLaBandejaVieneVaciaYNoSeConsultaNadaMas()
    {
        _salas.ListarDeUsuarioAsync(_ana.Id, Arg.Any<CancellationToken>()).Returns([]);

        Assert.Empty(await Manejador.ManejarAsync(new ConsultaSalasDeUsuario(_ana.Id)));

        await _mensajes.DidNotReceiveWithAnyArgs().ContarNoLeidosPorSalaAsync(default);
    }

    [Fact]
    public async Task UnaDirectaSePresentaConElNombreDelInterlocutor()
    {
        var directa = Datos.SalaDirecta(_ana, _eva);
        _salas.ListarDeUsuarioAsync(_ana.Id, Arg.Any<CancellationToken>()).Returns([directa]);

        var unica = Assert.Single(await Manejador.ManejarAsync(new ConsultaSalasDeUsuario(_ana.Id)));

        Assert.Equal("eva", unica.Nombre);
        Assert.True(unica.EsMiembro);
        Assert.Equal(2, unica.TotalMiembros);
    }

    [Fact]
    public async Task LaPresenciaDelInterlocutorSoloSeResuelveEnLasDirectas()
    {
        var directa = Datos.SalaDirecta(_ana, _eva);
        var grupo = Datos.Sala(nombre: "Equipo");
        grupo.Miembros.Add(Datos.Membresia(grupo.Id, _ana.Id, _ana));

        _salas.ListarDeUsuarioAsync(_ana.Id, Arg.Any<CancellationToken>()).Returns([directa, grupo]);
        _conexiones
            .FiltrarConectadosAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns<IReadOnlySet<Guid>>(_ => new HashSet<Guid> { _eva.Id });

        var resultado = await Manejador.ManejarAsync(new ConsultaSalasDeUsuario(_ana.Id));

        Assert.True(resultado.Single(s => s.EsDirecta).InterlocutorEnLinea);
        Assert.Null(resultado.Single(s => !s.EsDirecta).InterlocutorEnLinea);
    }

    [Fact]
    public async Task LosMensajesPendientesLleganPorSala()
    {
        var sala = Datos.Sala();
        sala.Miembros.Add(Datos.Membresia(sala.Id, _ana.Id, _ana));

        _salas.ListarDeUsuarioAsync(_ana.Id, Arg.Any<CancellationToken>()).Returns([sala]);
        _mensajes
            .ContarNoLeidosPorSalaAsync(_ana.Id, Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, int> { [sala.Id] = 7 });

        var unica = Assert.Single(await Manejador.ManejarAsync(new ConsultaSalasDeUsuario(_ana.Id)));

        Assert.Equal(7, unica.MensajesSinLeer);
    }

    [Fact]
    public async Task UnaSalaAlDiaNoTienePendientes()
    {
        var sala = Datos.Sala();
        _salas.ListarDeUsuarioAsync(_ana.Id, Arg.Any<CancellationToken>()).Returns([sala]);
        _mensajes
            .ContarNoLeidosPorSalaAsync(_ana.Id, Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, int>());

        Assert.Equal(0, Assert.Single(await Manejador.ManejarAsync(new ConsultaSalasDeUsuario(_ana.Id))).MensajesSinLeer);
    }

    [Fact]
    public async Task LaPrevisualizacionDelUltimoMensajeLlegaDescifrada()
    {
        var sala = Datos.Sala();
        _salas.ListarDeUsuarioAsync(_ana.Id, Arg.Any<CancellationToken>()).Returns([sala]);
        DevolverUltimo(new UltimoMensajeSala(sala.Id, _eva.Id, "eva", "abc", Datos.Ahora, null, null));

        var resumen = Assert.Single(await Manejador.ManejarAsync(new ConsultaSalasDeUsuario(_ana.Id))).UltimoMensaje;

        Assert.NotNull(resumen);
        Assert.Equal("claro(abc)", resumen.Texto);
        Assert.Equal("eva", resumen.NombreAutor);
        Assert.False(resumen.EsPropio);
    }

    [Fact]
    public async Task LaPrevisualizacionMarcaLosMensajesPropios()
    {
        var sala = Datos.Sala();
        _salas.ListarDeUsuarioAsync(_ana.Id, Arg.Any<CancellationToken>()).Returns([sala]);
        DevolverUltimo(new UltimoMensajeSala(sala.Id, _ana.Id, "ana", "abc", Datos.Ahora, null, null));

        var resumen = Assert.Single(await Manejador.ManejarAsync(new ConsultaSalasDeUsuario(_ana.Id))).UltimoMensaje;

        Assert.True(resumen!.EsPropio);
    }

    [Fact]
    public async Task LaPrevisualizacionSeRecortaEnElServidor()
    {
        // No tiene sentido mandar dos mil caracteres por la red para enseñar ciento veinte.
        var largo = new string('a', 500);
        _cifrador
            .IntentarDescifrar("largo", out Arg.Any<string?>())
            .Returns(llamada =>
            {
                llamada[1] = largo;
                return true;
            });

        var sala = Datos.Sala();
        _salas.ListarDeUsuarioAsync(_ana.Id, Arg.Any<CancellationToken>()).Returns([sala]);
        DevolverUltimo(new UltimoMensajeSala(sala.Id, _eva.Id, "eva", "largo", Datos.Ahora, null, null));

        var resumen = Assert.Single(await Manejador.ManejarAsync(new ConsultaSalasDeUsuario(_ana.Id))).UltimoMensaje;

        Assert.Equal(121, resumen!.Texto.Length);
        Assert.EndsWith("…", resumen.Texto, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnUltimoMensajeIlegibleSeResumeSinTexto()
    {
        _cifrador
            .IntentarDescifrar("roto", out Arg.Any<string?>())
            .Returns(llamada =>
            {
                llamada[1] = null;
                return false;
            });

        var sala = Datos.Sala();
        _salas.ListarDeUsuarioAsync(_ana.Id, Arg.Any<CancellationToken>()).Returns([sala]);
        DevolverUltimo(new UltimoMensajeSala(sala.Id, _eva.Id, "eva", "roto", Datos.Ahora, null, null));

        var resumen = Assert.Single(await Manejador.ManejarAsync(new ConsultaSalasDeUsuario(_ana.Id))).UltimoMensaje;

        Assert.Equal(string.Empty, resumen!.Texto);
    }

    [Fact]
    public async Task UnUltimoMensajeDeSoloArchivoConservaSuFicha()
    {
        var sala = Datos.Sala();
        _salas.ListarDeUsuarioAsync(_ana.Id, Arg.Any<CancellationToken>()).Returns([sala]);
        DevolverUltimo(new UltimoMensajeSala(
            sala.Id, _eva.Id, "eva", null, Datos.Ahora, "foto.png", TipoAdjunto.Imagen));

        var resumen = Assert.Single(await Manejador.ManejarAsync(new ConsultaSalasDeUsuario(_ana.Id))).UltimoMensaje;

        Assert.Equal(string.Empty, resumen!.Texto);
        Assert.Equal("foto.png", resumen.NombreAdjunto);
        Assert.Equal(TipoAdjunto.Imagen, resumen.TipoAdjunto);
    }

    [Fact]
    public async Task UnAutorBorradoSeResumeComoDesconocido()
    {
        var sala = Datos.Sala();
        _salas.ListarDeUsuarioAsync(_ana.Id, Arg.Any<CancellationToken>()).Returns([sala]);
        DevolverUltimo(new UltimoMensajeSala(sala.Id, _eva.Id, null, "abc", Datos.Ahora, null, null));

        var resumen = Assert.Single(await Manejador.ManejarAsync(new ConsultaSalasDeUsuario(_ana.Id))).UltimoMensaje;

        Assert.Equal("(desconocido)", resumen!.NombreAutor);
    }

    [Fact]
    public async Task LaBandejaSeResuelveEnUnNumeroFijoDeViajes()
    {
        // Cuatro consultas pase cual sea el número de conversaciones: si creciera con
        // ellas, la pantalla principal del cliente se volvería lenta con el uso.
        var salas = Enumerable.Range(0, 20).Select(i => Datos.Sala(nombre: $"Sala{i}")).ToArray();
        _salas.ListarDeUsuarioAsync(_ana.Id, Arg.Any<CancellationToken>()).Returns(salas);

        await Manejador.ManejarAsync(new ConsultaSalasDeUsuario(_ana.Id));

        await _salas.Received(1).ListarDeUsuarioAsync(_ana.Id, Arg.Any<CancellationToken>());
        await _mensajes.Received(1).ContarNoLeidosPorSalaAsync(_ana.Id, Arg.Any<CancellationToken>());
        await _mensajes.Received(1).ObtenerUltimosPorSalaAsync(
            Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>());
        await _conexiones.Received(1).FiltrarConectadosAsync(
            Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UnUsuarioSinIdentificarSeRechaza()
        => await Assert.ThrowsAsync<ExcepcionValidacion>(
            () => Manejador.ManejarAsync(new ConsultaSalasDeUsuario(Guid.Empty)));

    /// <summary>Programa el último mensaje que devolverá el repositorio.</summary>
    /// <param name="ultimo">Resumen del último mensaje.</param>
    private void DevolverUltimo(UltimoMensajeSala ultimo)
        => _mensajes
            .ObtenerUltimosPorSalaAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, UltimoMensajeSala> { [ultimo.SalaId] = ultimo });
}

/// <summary>Pruebas del listado de miembros de una sala.</summary>
public sealed class PruebasMiembrosSala
{
    private readonly IRepositorioSalas _salas = Substitute.For<IRepositorioSalas>();
    private readonly IRegistroConexiones _conexiones = Substitute.For<IRegistroConexiones>();

    private readonly Usuario _ana = Datos.Usuario(nombre: "ana");
    private readonly Usuario _eva = Datos.Usuario(nombre: "eva");
    private readonly Sala _sala;

    public PruebasMiembrosSala()
    {
        _sala = Datos.Sala(creadorId: _ana.Id);

        _salas.ObtenerPorIdAsync(_sala.Id, Arg.Any<CancellationToken>()).Returns(_sala);
        _salas.EsMiembroAsync(_sala.Id, _ana.Id, Arg.Any<CancellationToken>()).Returns(true);
        _salas.ListarMiembrosAsync(_sala.Id, Arg.Any<CancellationToken>()).Returns(
        [
            Datos.Membresia(_sala.Id, _ana.Id, _ana),
            Datos.Membresia(_sala.Id, _eva.Id, _eva)
        ]);

        _conexiones
            .FiltrarConectadosAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns<IReadOnlySet<Guid>>(_ => new HashSet<Guid> { _eva.Id });
    }

    private ManejadorMiembrosSala Manejador => new(_salas, _conexiones);

    [Fact]
    public async Task LosMiembrosLleganConSuPresenciaYSuAutoria()
    {
        var resultado = await Manejador.ManejarAsync(new ConsultaMiembrosSala(_sala.Id, _ana.Id));

        var ana = resultado.Single(m => m.NombreUsuario == "ana");
        var eva = resultado.Single(m => m.NombreUsuario == "eva");

        Assert.True(ana.EsCreador);
        Assert.False(ana.EnLinea);
        Assert.False(eva.EsCreador);
        Assert.True(eva.EnLinea);
    }

    [Fact]
    public async Task LaPresenciaSePideDeUnaVezParaTodosLosMiembros()
    {
        // Con el registro compartido, preguntar uno a uno sería una ida y vuelta por miembro.
        await Manejador.ManejarAsync(new ConsultaMiembrosSala(_sala.Id, _ana.Id));

        await _conexiones.Received(1).FiltrarConectadosAsync(
            Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.Count == 2),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SoloLosMiembrosVenQuienComponeLaSala()
    {
        var intruso = Guid.CreateVersion7();
        _salas.EsMiembroAsync(_sala.Id, intruso, Arg.Any<CancellationToken>()).Returns(false);

        await Assert.ThrowsAsync<ExcepcionAutorizacion>(
            () => Manejador.ManejarAsync(new ConsultaMiembrosSala(_sala.Id, intruso)));
    }

    [Fact]
    public async Task UnAdministradorPuedeVerLosMiembrosSinPertenecer()
    {
        var auditor = Guid.CreateVersion7();
        _salas.EsMiembroAsync(_sala.Id, auditor, Arg.Any<CancellationToken>()).Returns(false);

        var resultado = await Manejador.ManejarAsync(
            new ConsultaMiembrosSala(_sala.Id, auditor, OmitirComprobacionMembresia: true));

        Assert.Equal(2, resultado.Count);
    }

    [Fact]
    public async Task UnMiembroSinUsuarioCargadoSeMuestraComoDesconocido()
    {
        _salas
            .ListarMiembrosAsync(_sala.Id, Arg.Any<CancellationToken>())
            .Returns([Datos.Membresia(_sala.Id, Guid.CreateVersion7())]);

        var unico = Assert.Single(await Manejador.ManejarAsync(new ConsultaMiembrosSala(_sala.Id, _ana.Id)));

        Assert.Equal("(desconocido)", unico.NombreUsuario);
    }

    [Fact]
    public async Task UnaSalaInexistenteSeRechaza()
    {
        var salaId = Guid.CreateVersion7();
        _salas.ObtenerPorIdAsync(salaId, Arg.Any<CancellationToken>()).Returns((Sala?)null);

        await Assert.ThrowsAsync<ExcepcionNoEncontrado>(
            () => Manejador.ManejarAsync(new ConsultaMiembrosSala(salaId, _ana.Id)));
    }

    [Fact]
    public async Task UnIdentificadorVacioSeRechaza()
        => await Assert.ThrowsAsync<ExcepcionValidacion>(
            () => Manejador.ManejarAsync(new ConsultaMiembrosSala(Guid.Empty, _ana.Id)));
}
