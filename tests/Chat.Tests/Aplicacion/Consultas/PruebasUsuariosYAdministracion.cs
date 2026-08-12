using Chat.Aplicacion.Abstracciones;
using Chat.Aplicacion.Consultas.Administracion;
using Chat.Aplicacion.Consultas.Usuarios;
using Chat.Aplicacion.Dtos;
using Chat.Dominio.Abstracciones;
using Chat.Dominio.Entidades;
using Chat.Tests.Comun;
using NSubstitute;

namespace Chat.Tests.Aplicacion.Consultas;

/// <summary>Pruebas del listado de usuarios y de su interacción con la caché.</summary>
public sealed class PruebasListarUsuarios
{
    private readonly IRepositorioUsuarios _usuarios = Substitute.For<IRepositorioUsuarios>();
    private readonly IRegistroConexiones _conexiones = Substitute.For<IRegistroConexiones>();
    private readonly CacheDePrueba _cache = new();

    private readonly Usuario _ana = Datos.Usuario(nombre: "ana");
    private readonly Usuario _eva = Datos.Usuario(nombre: "eva");

    public PruebasListarUsuarios()
    {
        _usuarios.ListarAsync(false, Arg.Any<CancellationToken>()).Returns([_ana, _eva]);
        _usuarios.ListarAsync(true, Arg.Any<CancellationToken>()).Returns(
            [_ana, _eva, Datos.Usuario(nombre: "leo", activo: false)]);

        _conexiones
            .FiltrarConectadosAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns<IReadOnlySet<Guid>>(_ => new HashSet<Guid>());
    }

    private ManejadorListarUsuarios Manejador => new(
        _usuarios,
        _cache,
        _conexiones,
        Opciones.De(Opciones.Cache()));

    [Fact]
    public async Task LosUsuariosLleganProyectadosSinDatosSensibles()
    {
        var resultado = await Manejador.ManejarAsync(new ConsultaListarUsuarios());

        Assert.Equal(["ana", "eva"], resultado.Select(u => u.NombreUsuario));
        Assert.Equal("ana@dotchat.local", resultado[0].Email);
        Assert.True(resultado[0].Activo);
    }

    [Fact]
    public async Task ElListadoDeActivosYElDeTodosSeCacheanPorSeparado()
    {
        // Comparten caché pero no clave: si la compartieran, pedir uno serviría el otro.
        var activos = await Manejador.ManejarAsync(new ConsultaListarUsuarios());
        var todos = await Manejador.ManejarAsync(new ConsultaListarUsuarios(IncluirInactivos: true));

        Assert.Equal(2, activos.Count);
        Assert.Equal(3, todos.Count);
        Assert.Equal(2, _cache.Generaciones);
        Assert.True(_cache.Contiene(ClavesCache.ListaUsuarios(false)));
        Assert.True(_cache.Contiene(ClavesCache.ListaUsuarios(true)));
    }

    [Fact]
    public async Task LaSegundaConsultaSeSirveDeLaCache()
    {
        await Manejador.ManejarAsync(new ConsultaListarUsuarios());
        await Manejador.ManejarAsync(new ConsultaListarUsuarios());

        Assert.Equal(1, _cache.Generaciones);
        await _usuarios.Received(1).ListarAsync(false, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LaPresenciaSeResuelveFueraDeLaCacheEnCadaConsulta()
    {
        // Es un dato volátil: si quedara congelado dentro de la entrada cacheada, el
        // cliente enseñaría a alguien «en línea» minutos después de haberse ido.
        await Manejador.ManejarAsync(new ConsultaListarUsuarios());

        _conexiones
            .FiltrarConectadosAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns<IReadOnlySet<Guid>>(_ => new HashSet<Guid> { _eva.Id });

        var segunda = await Manejador.ManejarAsync(new ConsultaListarUsuarios());

        Assert.False(segunda.Single(u => u.NombreUsuario == "ana").EnLinea);
        Assert.True(segunda.Single(u => u.NombreUsuario == "eva").EnLinea);
        Assert.Equal(1, _cache.Generaciones);
    }

    [Fact]
    public async Task LaPresenciaSePideDeUnaVezParaTodoElListado()
    {
        await Manejador.ManejarAsync(new ConsultaListarUsuarios());

        await _conexiones.Received(1).FiltrarConectadosAsync(
            Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.Count == 2),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UnaConsultaNulaSeRechaza()
        => await Assert.ThrowsAsync<ArgumentNullException>(() => Manejador.ManejarAsync(null!));
}

/// <summary>Pruebas de las consultas que solo delegan en el registro de conexiones.</summary>
public sealed class PruebasPresenciaYConexiones
{
    private readonly IRegistroConexiones _conexiones = Substitute.For<IRegistroConexiones>();

    [Fact]
    public async Task LaPresenciaSeDevuelveTalComoLaEntregaElRegistro()
    {
        IReadOnlyList<PresenciaDto> presencias =
            [new PresenciaDto(Guid.CreateVersion7(), "ana", true, Datos.Ahora, 2)];

        _conexiones.ListarPresenciaAsync(Arg.Any<CancellationToken>()).Returns(presencias);

        var resultado = await new ManejadorPresencia(_conexiones).ManejarAsync(new ConsultaPresencia());

        Assert.Same(presencias, resultado);
    }

    [Fact]
    public async Task LasConexionesActivasSeDevuelvenTalComoLasEntregaElRegistro()
    {
        IReadOnlyList<ConexionActivaDto> conexiones =
            [new ConexionActivaDto("c1", Guid.CreateVersion7(), "ana", Datos.Ahora, ["General"])];

        _conexiones.ListarAsync(Arg.Any<CancellationToken>()).Returns(conexiones);

        var resultado = await new ManejadorConexionesActivas(_conexiones)
            .ManejarAsync(new ConsultaConexionesActivas());

        Assert.Same(conexiones, resultado);
    }

    [Fact]
    public async Task UnaConsultaNulaSeRechaza()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => new ManejadorPresencia(_conexiones).ManejarAsync(null!));

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => new ManejadorConexionesActivas(_conexiones).ManejarAsync(null!));
    }
}

/// <summary>Pruebas del resumen de actividad de la plataforma.</summary>
public sealed class PruebasEstadisticas
{
    private readonly IRepositorioUsuarios _usuarios = Substitute.For<IRepositorioUsuarios>();
    private readonly IRepositorioSalas _salas = Substitute.For<IRepositorioSalas>();
    private readonly IRepositorioMensajes _mensajes = Substitute.For<IRepositorioMensajes>();
    private readonly IRepositorioAdjuntos _adjuntos = Substitute.For<IRepositorioAdjuntos>();
    private readonly IRegistroConexiones _conexiones = Substitute.For<IRegistroConexiones>();
    private readonly RelojFijo _reloj = new();

    [Fact]
    public async Task ElResumenReuneLosRecuentosDeCadaOrigen()
    {
        _usuarios.ContarAsync(Arg.Any<CancellationToken>()).Returns(12);
        _salas.ContarAsync(Arg.Any<CancellationToken>()).Returns(4);
        _mensajes.ContarAsync(null, Arg.Any<CancellationToken>()).Returns(340);
        _adjuntos.ContarAsync(Arg.Any<CancellationToken>()).Returns(9);
        _adjuntos.SumarTamanoAsync(Arg.Any<CancellationToken>()).Returns(4096L);
        _conexiones.ContarConexionesAsync(Arg.Any<CancellationToken>()).Returns(6);
        _conexiones.ContarUsuariosConectadosAsync(Arg.Any<CancellationToken>()).Returns(3);

        var resultado = await Manejador.ManejarAsync(new ConsultaEstadisticas());

        Assert.Equal(12, resultado.TotalUsuarios);
        Assert.Equal(4, resultado.TotalSalas);
        Assert.Equal(340, resultado.TotalMensajes);
        Assert.Equal(9, resultado.TotalAdjuntos);
        Assert.Equal(4096L, resultado.BytesAdjuntos);
        Assert.Equal(6, resultado.ConexionesActivas);
        Assert.Equal(3, resultado.UsuariosConectados);
        Assert.Equal(_reloj.Ahora, resultado.FechaConsulta);
    }

    [Fact]
    public async Task UnaPlataformaReciénInstaladaDevuelveTodoACero()
    {
        var resultado = await Manejador.ManejarAsync(new ConsultaEstadisticas());

        Assert.Equal(0, resultado.TotalUsuarios);
        Assert.Equal(0L, resultado.BytesAdjuntos);
    }

    [Fact]
    public async Task UnaConsultaNulaSeRechaza()
        => await Assert.ThrowsAsync<ArgumentNullException>(() => Manejador.ManejarAsync(null!));

    private ManejadorEstadisticas Manejador
        => new(_usuarios, _salas, _mensajes, _adjuntos, _conexiones, _reloj);
}
