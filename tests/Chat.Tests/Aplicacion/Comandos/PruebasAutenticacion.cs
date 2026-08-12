using Chat.Aplicacion.Abstracciones;
using Chat.Aplicacion.Comandos.Autenticacion;
using Chat.Aplicacion.Dtos;
using Chat.Dominio.Abstracciones;
using Chat.Dominio.Constantes;
using Chat.Dominio.Entidades;
using Chat.Dominio.Excepciones;
using Chat.Tests.Comun;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Chat.Tests.Aplicacion.Comandos;

/// <summary>Pruebas del inicio de sesión.</summary>
public sealed class PruebasIniciarSesion
{
    private readonly IRepositorioUsuarios _usuarios = Substitute.For<IRepositorioUsuarios>();
    private readonly IServicioIdentidad _identidad = Substitute.For<IServicioIdentidad>();
    private readonly IEmisorSesiones _emisor = Substitute.For<IEmisorSesiones>();

    private ManejadorIniciarSesion Manejador => new(
        _usuarios,
        _identidad,
        _emisor,
        NullLogger<ManejadorIniciarSesion>.Instance);

    private static ComandoIniciarSesion Comando(string nombre = "ana", string clave = "clave-larga-1")
        => new(new SolicitudLoginDto(nombre, clave));

    [Fact]
    public async Task ConCredencialesCorrectasSeEmiteLaSesion()
    {
        var usuario = Datos.Usuario();
        var sesion = new RespuestaAutenticacionDto(
            usuario.Id, "ana", "token", Datos.Ahora.AddMinutes(30), "refresco", [RolesDelSistema.Usuario]);

        _usuarios.ObtenerPorNombreAsync("ana", Arg.Any<CancellationToken>()).Returns(usuario);
        _identidad.VerificarClaveAsync(usuario, "clave-larga-1", Arg.Any<CancellationToken>()).Returns(true);
        _emisor.EmitirAsync(usuario, Arg.Any<CancellationToken>()).Returns(sesion);

        Assert.Same(sesion, await Manejador.ManejarAsync(Comando()));
    }

    [Fact]
    public async Task UnUsuarioInexistenteYUnaClaveIncorrectaDanElMismoError()
    {
        // El mensaje es deliberadamente idéntico: distinguirlos revelaría qué nombres
        // de usuario existen en la plataforma.
        var usuario = Datos.Usuario();

        _usuarios.ObtenerPorNombreAsync("ana", Arg.Any<CancellationToken>()).Returns((Usuario?)null);
        var inexistente = await Assert.ThrowsAsync<ExcepcionAutenticacion>(() => Manejador.ManejarAsync(Comando()));

        _usuarios.ObtenerPorNombreAsync("ana", Arg.Any<CancellationToken>()).Returns(usuario);
        _identidad.VerificarClaveAsync(usuario, Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);
        var claveMala = await Assert.ThrowsAsync<ExcepcionAutenticacion>(() => Manejador.ManejarAsync(Comando()));

        Assert.Equal(inexistente.Message, claveMala.Message);
    }

    [Fact]
    public async Task UnaCuentaDesactivadaNoPuedeIniciarSesion()
    {
        _usuarios
            .ObtenerPorNombreAsync("ana", Arg.Any<CancellationToken>())
            .Returns(Datos.Usuario(activo: false));

        await Assert.ThrowsAsync<ExcepcionAutenticacion>(() => Manejador.ManejarAsync(Comando()));

        // Ni siquiera se llega a comprobar la contraseña.
        await _identidad.DidNotReceiveWithAnyArgs().VerificarClaveAsync(default!, default!);
    }

    [Fact]
    public async Task NoSeEmiteSesionCuandoLaClaveNoEsCorrecta()
    {
        var usuario = Datos.Usuario();

        _usuarios.ObtenerPorNombreAsync("ana", Arg.Any<CancellationToken>()).Returns(usuario);
        _identidad.VerificarClaveAsync(usuario, Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);

        await Assert.ThrowsAsync<ExcepcionAutenticacion>(() => Manejador.ManejarAsync(Comando()));
        await _emisor.DidNotReceiveWithAnyArgs().EmitirAsync(default!);
    }

    [Fact]
    public async Task UnNombreDeUsuarioMalFormadoNiSiquieraLlegaALaBaseDeDatos()
    {
        await Assert.ThrowsAsync<ExcepcionValidacion>(() => Manejador.ManejarAsync(Comando(nombre: "a")));
        await _usuarios.DidNotReceiveWithAnyArgs().ObtenerPorNombreAsync(default!);
    }

    [Fact]
    public async Task UnaClaveDemasiadoCortaSeRechazaAntesDeConsultar()
    {
        await Assert.ThrowsAsync<ExcepcionValidacion>(() => Manejador.ManejarAsync(Comando(clave: "corta")));
        await _usuarios.DidNotReceiveWithAnyArgs().ObtenerPorNombreAsync(default!);
    }

    [Fact]
    public async Task UnComandoNuloSeRechaza()
        => await Assert.ThrowsAsync<ArgumentNullException>(() => Manejador.ManejarAsync(null!));
}

/// <summary>Pruebas del alta de cuentas.</summary>
public sealed class PruebasRegistrarUsuario
{
    private readonly IRepositorioUsuarios _usuarios = Substitute.For<IRepositorioUsuarios>();
    private readonly IServicioIdentidad _identidad = Substitute.For<IServicioIdentidad>();
    private readonly IEmisorSesiones _emisor = Substitute.For<IEmisorSesiones>();
    private readonly CacheDePrueba _cache = new();
    private readonly RelojFijo _reloj = new();

    public PruebasRegistrarUsuario()
    {
        _identidad.CrearUsuarioAsync(Arg.Any<Usuario>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ResultadoIdentidad.Correcto);

        _identidad.AsignarRolAsync(Arg.Any<Usuario>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ResultadoIdentidad.Correcto);

        _emisor.EmitirAsync(Arg.Any<Usuario>(), Arg.Any<CancellationToken>())
            .Returns(llamada => new RespuestaAutenticacionDto(
                llamada.Arg<Usuario>().Id,
                llamada.Arg<Usuario>().UserName!,
                "token",
                Datos.Ahora.AddMinutes(30),
                "refresco",
                [RolesDelSistema.Usuario]));
    }

    private ManejadorRegistrarUsuario Manejador => new(
        _usuarios,
        _identidad,
        _emisor,
        _cache,
        _reloj,
        NullLogger<ManejadorRegistrarUsuario>.Instance);

    private static ComandoRegistrarUsuario Comando(
        string nombre = "ana",
        string email = "Ana@DotChat.Local",
        string clave = "clave-larga-1")
        => new(new SolicitudRegistroDto(nombre, email, clave));

    [Fact]
    public async Task UnaCuentaNuevaSeCreaConSuRolYDevuelveLaSesionIniciada()
    {
        var sesion = await Manejador.ManejarAsync(Comando());

        var creado = _identidad.ReceivedCalls()
            .First(llamada => llamada.GetMethodInfo().Name == nameof(IServicioIdentidad.CrearUsuarioAsync))
            .GetArguments()[0] as Usuario;

        Assert.NotNull(creado);
        Assert.Equal("ana", creado.UserName);
        Assert.Equal("ana@dotchat.local", creado.Email);
        Assert.Equal(Datos.Ahora, creado.FechaCreacion);
        Assert.True(creado.Activo);

        await _identidad.Received(1).AsignarRolAsync(creado, RolesDelSistema.Usuario, Arg.Any<CancellationToken>());
        Assert.Equal("token", sesion.TokenAcceso);
    }

    [Fact]
    public async Task ElAltaInvalidaElListadoCacheadoDeUsuarios()
    {
        await Manejador.ManejarAsync(Comando());

        Assert.Contains(ClavesCache.EtiquetaUsuarios, _cache.EtiquetasInvalidadas);
    }

    [Fact]
    public async Task UnNombreYaTomadoSeRechaza()
    {
        _usuarios.ObtenerPorNombreAsync("ana", Arg.Any<CancellationToken>()).Returns(Datos.Usuario());

        var excepcion = await Assert.ThrowsAsync<ExcepcionConflicto>(() => Manejador.ManejarAsync(Comando()));

        Assert.Contains("ana", excepcion.Message, StringComparison.Ordinal);
        await _identidad.DidNotReceiveWithAnyArgs().CrearUsuarioAsync(default!, default!);
    }

    [Fact]
    public async Task UnCorreoYaRegistradoSeRechaza()
    {
        _identidad.ExisteEmailAsync("ana@dotchat.local", Arg.Any<CancellationToken>()).Returns(true);

        await Assert.ThrowsAsync<ExcepcionConflicto>(() => Manejador.ManejarAsync(Comando()));
        await _identidad.DidNotReceiveWithAnyArgs().CrearUsuarioAsync(default!, default!);
    }

    [Fact]
    public async Task LosErroresDePoliticaDeContrasenaLleganAlCliente()
    {
        _identidad.CrearUsuarioAsync(Arg.Any<Usuario>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ResultadoIdentidad.Fallido("Falta un dígito.", "Falta un símbolo."));

        var excepcion = await Assert.ThrowsAsync<ExcepcionValidacion>(() => Manejador.ManejarAsync(Comando()));

        Assert.Equal(["Falta un dígito.", "Falta un símbolo."], excepcion.Errores["clave"]);
    }

    [Fact]
    public async Task SiFallaLaAsignacionDeRolElAltaSeConsideraEnConflicto()
    {
        _identidad.AsignarRolAsync(Arg.Any<Usuario>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ResultadoIdentidad.Fallido("El rol no existe."));

        var excepcion = await Assert.ThrowsAsync<ExcepcionConflicto>(() => Manejador.ManejarAsync(Comando()));

        Assert.Contains("El rol no existe.", excepcion.Message, StringComparison.Ordinal);
        await _emisor.DidNotReceiveWithAnyArgs().EmitirAsync(default!);
    }

    [Theory]
    [InlineData("a", "ana@dotchat.local", "clave-larga-1")]
    [InlineData("ana", "correo-invalido", "clave-larga-1")]
    [InlineData("ana", "ana@dotchat.local", "corta")]
    public async Task LosDatosDeAltaSeValidanAntesDeTocarNada(string nombre, string email, string clave)
    {
        await Assert.ThrowsAsync<ExcepcionValidacion>(
            () => Manejador.ManejarAsync(Comando(nombre, email, clave)));

        await _usuarios.DidNotReceiveWithAnyArgs().ObtenerPorNombreAsync(default!);
    }
}

/// <summary>Pruebas de la renovación de sesión con rotación de tokens.</summary>
public sealed class PruebasRefrescarSesion
{
    private const string TokenEnClaro = "token-de-refresco";
    private const string Hash = "hash-del-token";

    private readonly IRepositorioTokensRefresco _tokens = Substitute.For<IRepositorioTokensRefresco>();
    private readonly IRepositorioUsuarios _usuarios = Substitute.For<IRepositorioUsuarios>();
    private readonly IGeneradorTokens _generador = Substitute.For<IGeneradorTokens>();
    private readonly IEmisorSesiones _emisor = Substitute.For<IEmisorSesiones>();
    private readonly IUnidadDeTrabajo _unidadDeTrabajo = Substitute.For<IUnidadDeTrabajo>();
    private readonly RelojFijo _reloj = new();

    public PruebasRefrescarSesion()
        => _generador.CalcularHashRefresco(TokenEnClaro).Returns(Hash);

    private ManejadorRefrescarSesion Manejador => new(
        _tokens,
        _usuarios,
        _generador,
        _emisor,
        _unidadDeTrabajo,
        _reloj,
        NullLogger<ManejadorRefrescarSesion>.Instance);

    private static ComandoRefrescarSesion Comando(string token = TokenEnClaro)
        => new(new SolicitudRefrescoDto(token));

    [Fact]
    public async Task UnTokenValidoSeCanjeaYQuedaRevocadoEnElActo()
    {
        var usuario = Datos.Usuario();
        var almacenado = Datos.TokenRefresco(usuario.Id, Hash, _reloj.Ahora.AddDays(3));
        var sesion = new RespuestaAutenticacionDto(
            usuario.Id, "ana", "token-nuevo", _reloj.Ahora.AddMinutes(30), "refresco-nuevo", []);

        _tokens.ObtenerPorHashAsync(Hash, Arg.Any<CancellationToken>()).Returns(almacenado);
        _usuarios.ObtenerPorIdAsync(usuario.Id, Arg.Any<CancellationToken>()).Returns(usuario);
        _emisor.EmitirAsync(usuario, Arg.Any<CancellationToken>()).Returns(sesion);

        var resultado = await Manejador.ManejarAsync(Comando());

        // La rotación es lo que hace que un token robado solo sirva una vez.
        Assert.Same(sesion, resultado);
        Assert.True(almacenado.EstaRevocado);
        Assert.Equal(_reloj.Ahora, almacenado.FechaRevocacion);
    }

    [Fact]
    public async Task UnTokenDesconocidoSeRechaza()
    {
        _tokens.ObtenerPorHashAsync(Hash, Arg.Any<CancellationToken>()).Returns((TokenRefresco?)null);

        await Assert.ThrowsAsync<ExcepcionAutenticacion>(() => Manejador.ManejarAsync(Comando()));
    }

    [Fact]
    public async Task UnTokenCaducadoSeRechazaSinCerrarLasDemasSesiones()
    {
        var caducado = Datos.TokenRefresco(Guid.CreateVersion7(), Hash, _reloj.Ahora.AddSeconds(-1));
        _tokens.ObtenerPorHashAsync(Hash, Arg.Any<CancellationToken>()).Returns(caducado);

        await Assert.ThrowsAsync<ExcepcionAutenticacion>(() => Manejador.ManejarAsync(Comando()));

        // Caducar es normal; solo la reutilización de uno revocado es sospechosa.
        await _tokens.DidNotReceiveWithAnyArgs().RevocarTodosAsync(default, default);
    }

    [Fact]
    public async Task ReutilizarUnTokenYaCanjeadoCierraTodasLasSesionesDelUsuario()
    {
        // Presentar un token revocado significa que alguien tiene una copia: se cortan
        // todas las sesiones por precaución, incluida la del ladrón.
        var usuarioId = Guid.CreateVersion7();
        var revocado = Datos.TokenRefresco(
            usuarioId, Hash, _reloj.Ahora.AddDays(3), revocacion: _reloj.Ahora.AddMinutes(-5));

        _tokens.ObtenerPorHashAsync(Hash, Arg.Any<CancellationToken>()).Returns(revocado);

        await Assert.ThrowsAsync<ExcepcionAutenticacion>(() => Manejador.ManejarAsync(Comando()));

        await _tokens.Received(1).RevocarTodosAsync(usuarioId, _reloj.Ahora, Arg.Any<CancellationToken>());
        await _unidadDeTrabajo.Received(1).GuardarCambiosAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UnTokenDeUnaCuentaDesactivadaNoRenuevaNada()
    {
        var usuario = Datos.Usuario(activo: false);
        _tokens
            .ObtenerPorHashAsync(Hash, Arg.Any<CancellationToken>())
            .Returns(Datos.TokenRefresco(usuario.Id, Hash, _reloj.Ahora.AddDays(3)));
        _usuarios.ObtenerPorIdAsync(usuario.Id, Arg.Any<CancellationToken>()).Returns(usuario);

        await Assert.ThrowsAsync<ExcepcionAutenticacion>(() => Manejador.ManejarAsync(Comando()));
        await _emisor.DidNotReceiveWithAnyArgs().EmitirAsync(default!);
    }

    [Fact]
    public async Task UnTokenDeUnUsuarioBorradoNoRenuevaNada()
    {
        var usuarioId = Guid.CreateVersion7();
        _tokens
            .ObtenerPorHashAsync(Hash, Arg.Any<CancellationToken>())
            .Returns(Datos.TokenRefresco(usuarioId, Hash, _reloj.Ahora.AddDays(3)));
        _usuarios.ObtenerPorIdAsync(usuarioId, Arg.Any<CancellationToken>()).Returns((Usuario?)null);

        await Assert.ThrowsAsync<ExcepcionAutenticacion>(() => Manejador.ManejarAsync(Comando()));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task UnTokenVacioEsUnErrorDeValidacion(string token)
        => await Assert.ThrowsAsync<ExcepcionValidacion>(() => Manejador.ManejarAsync(Comando(token)));
}
